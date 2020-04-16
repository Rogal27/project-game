﻿using Messaging.Contracts;
using Messaging.Contracts.Agent;
using Messaging.Contracts.Errors;
using Messaging.Contracts.GameMaster;
using Messaging.Enumerators;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Agent
{
    public class ProcessMessages
    {
        private Agent agent;
        private static NLog.Logger logger; 

        public ProcessMessages(Agent agent)
        {
            this.agent = agent;
            logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public ActionResult Process(Message<CheckShamResponse> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process check scham response: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (message.Payload.Sham)
            {
                logger.Info("Process check scham response: Agent checked sham and destroy piece." + " AgentID: " + agent.id.ToString());
                return agent.DestroyPiece();
            }
            else
            {
                logger.Info("Process check scham response: Agent checked not sham." + " AgentID: " + agent.id.ToString());
                agent.Piece.isDiscovered = true;
                return agent.MakeDecisionFromStrategy();
            }
        }

        public ActionResult Process(Message<DestroyPieceResponse> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process destroy piece response: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            agent.Piece = null;
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<DiscoverResponse> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process discover response: Agent not in game." + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            DateTime now = DateTime.Now;
            for (int y = agent.StartGameComponent.position.Y - 1; y <= agent.StartGameComponent.position.Y + 1; y++)
            {
                for (int x = agent.StartGameComponent.position.X - 1; x <= agent.StartGameComponent.position.X + 1; x++)
                {
                    int taby = y - agent.StartGameComponent.position.Y + 1;
                    int tabx = x - agent.StartGameComponent.position.X + 1;
                    if (Common.OnBoard(new Point(x, y), agent.BoardLogicComponent.boardSize))
                    {
                        agent.BoardLogicComponent.board[y, x].distToPiece = message.Payload.Distances[taby, tabx];
                        agent.BoardLogicComponent.board[y, x].distLearned = now;
                    }
                }
            }
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<ExchangeInformationResponseForward> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process exchange information response: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            agent.BoardLogicComponent.UpdateDistances(message.Payload.Distances);
            agent.BoardLogicComponent.UpdateBlueTeamGoalAreaInformation(message.Payload.BlueTeamGoalAreaInformation);
            agent.BoardLogicComponent.UpdateRedTeamGoalAreaInformation(message.Payload.RedTeamGoalAreaInformation);
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<MoveResponse> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process move response: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            agent.StartGameComponent.position = message.Payload.CurrentPosition;
            if (message.Payload.MadeMove)
            {
                agent.DeniedLastMove = false;
                agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distToPiece = message.Payload.ClosestPiece;
                agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distLearned = DateTime.Now;
                if (message.Payload.ClosestPiece == 0/* && board[position.Y, position.X].goalInfo == GoalInformation.NoInformation*/)
                {
                    logger.Info("Process move response: agent pick up piece." + " AgentID: " + agent.id.ToString());
                    return agent.PickUp();
                }
            }
            else
            {
                agent.DeniedLastMove = true;
                logger.Info("Process move response: agent did not move." + " AgentID: " + agent.id.ToString());
                var deniedField = Common.GetFieldInDirection(agent.StartGameComponent.position, agent.LastDirection);
                if (Common.OnBoard(deniedField, agent.BoardLogicComponent.boardSize)) agent.BoardLogicComponent.board[deniedField.Y, deniedField.X].deniedMove = DateTime.Now;
            }
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<PickUpPieceResponse> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process pick up piece response: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distToPiece == 0)
            {
                logger.Info("Process pick up piece response: Agent picked up piece" + " AgentID: " + agent.id.ToString());
                agent.Piece = new Piece();
            }
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<PutDownPieceResponse> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process put down piece response: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            agent.Piece = null;
            switch (message.Payload.Result)
            {
                case PutDownPieceResult.NormalOnGoalField:
                    agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].goalInfo = GoalInformation.Goal;
                    break;
                case PutDownPieceResult.NormalOnNonGoalField:
                    agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].goalInfo = GoalInformation.NoGoal;
                    break;
                case PutDownPieceResult.ShamOnGoalArea:
                    break;
                case PutDownPieceResult.TaskField:
                    agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].goalInfo = GoalInformation.NoGoal;
                    agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distToPiece = 0;
                    agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distLearned = DateTime.Now;
                    break;
            }
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<ExchangeInformationRequestForward> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process exchange information payload: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (message.Payload.Leader)
            {
                logger.Info("Process exchange information payload: Agent give info to leader" + " AgentID: " + agent.id.ToString());
                return agent.GiveInfo(message.Payload.AskingAgentId);
            }
            else
            {
                agent.WaitingPlayers.Add(message.Payload.AskingAgentId);
                return agent.MakeDecisionFromStrategy();
            }
        }

        public ActionResult Process(Message<JoinResponse> message)
        {
            if (agent.AgentState != AgentState.WaitingForJoin)
            {
                logger.Warn("Process join response: Agent not waiting for join" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (message.Payload.Accepted)
            {
                bool wasWaiting = agent.AgentState == AgentState.WaitingForJoin;
                agent.AgentState = AgentState.WaitingForStart;
                agent.id = message.Payload.AgentId;
                return wasWaiting ? ActionResult.Continue : agent.MakeDecisionFromStrategy();
            }
            else
            {
                logger.Info("Process join response: Join request not accepted" + " AgentID: " + agent.id.ToString());
                return ActionResult.Finish;
            }
        }

        public ActionResult Process(Message<StartGamePayload> message)
        {
            if (agent.AgentState != AgentState.WaitingForStart)
            {
                logger.Warn("Process start game payload: Agent not waiting for startjoin" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            agent.StartGameComponent.Initialize(message.Payload);
            if (agent.id != message.Payload.AgentId)
            {
                logger.Warn("Process start game payload: payload.agnetId not equal agentId" + " AgentID: " + agent.id.ToString());
            }
            agent.AgentState = AgentState.InGame;
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<EndGamePayload> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process end game payload: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Info("Process End Game: end game" + " AgentID: " + agent.id.ToString());
            return ActionResult.Finish;
        }

        public ActionResult Process(Message<IgnoredDelayError> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process ignoreed delay error: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("IgnoredDelay error" + " AgentID: " + agent.id.ToString());
            var time = message.Payload.RemainingDelay;
            agent.RemainingPenalty = Math.Max(0.0, time.TotalSeconds);
            return ActionResult.Continue;
        }

        public ActionResult Process(Message<MoveError> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process move error: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("Move error" + " AgentID: " + agent.id.ToString());
            agent.DeniedLastMove = true;
            agent.StartGameComponent.position = message.Payload.Position;
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<PickUpPieceError> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process pick up piece error: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("Pick up piece error" + " AgentID: " + agent.id.ToString());
            agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distLearned = DateTime.Now;
            agent.BoardLogicComponent.board[agent.StartGameComponent.position.Y, agent.StartGameComponent.position.X].distToPiece = int.MaxValue;
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<PutDownPieceError> message)
        {
            if (agent.AgentState != AgentState.InGame)
            {
                logger.Warn("Process put down piece error: Agent not in game" + " AgentID: " + agent.id.ToString());
                if (agent.endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("Put down piece error" + " AgentID: " + agent.id.ToString());
            if (message.Payload.ErrorSubtype == PutDownPieceErrorSubtype.AgentNotHolding) agent.Piece = null;
            return agent.MakeDecisionFromStrategy();
        }

        public ActionResult Process(Message<UndefinedError> message)
        {
            logger.Warn("Undefined error" + " AgentID: " + agent.id.ToString());
            return agent.MakeDecisionFromStrategy();
        }
    }
}
