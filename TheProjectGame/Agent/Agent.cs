﻿using Agent.Interfaces;
using Agent.strategies;
using Messaging.Contracts;
using Messaging.Contracts.Agent;
using Messaging.Contracts.Errors;
using Messaging.Contracts.GameMaster;
using Messaging.Enumerators;
using Messaging.Implementation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

namespace Agent
{
    public class Agent : IMessageProcessor
    {
        private const bool endIfUnexpectedMessage = false;

        private const bool endIfUnexpectedAction = false;

        private const int maxSkipCount = int.MaxValue;

        private int skipCount;

        public int id;

        private int lastAskedTeammate;

        public Direction lastDirection;

        private ISender sender;

        private IStrategy strategy;

        private List<BaseMessage> injectedMessages;

        private double remainingPenalty;

        public TeamId team;

        public bool isLeader;

        public bool wantsToBeLeader;

        public Field[,] board;

        public Point boardSize;

        public int goalAreaSize;

        public Point position;

        public List<int> waitingPlayers;

        public int[] teamMates;

        public Dictionary<ActionType, TimeSpan> penalties;

        public int averageTime;

        public float shamPieceProbability;

        public Piece piece;

        public AgentState agentState;

        private static NLog.Logger logger;

        public string CsIP;

        public string CsPort;

        public bool deniedLastMove;

        public Action<Agent, BaseMessage> MockMessageSendFunction { get; set; }

        public Agent(bool wantsToBeLeader = false)
        {
            this.wantsToBeLeader = wantsToBeLeader;
            piece = null;
            lastAskedTeammate = 0;
            deniedLastMove = false;
            remainingPenalty = 0.0;
            skipCount = 0;
            waitingPlayers = new List<int>();
            strategy = new SimpleStrategy();
            injectedMessages = new List<BaseMessage>();
            agentState = AgentState.Created;
            logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public void Initialize(int leaderId, TeamId teamId, Point boardSize, int goalAreaHeight, Point pos, int[] alliesIds, Dictionary<ActionType, TimeSpan> penalties, float shamPieceProbability)
        {
            isLeader = id == leaderId ? true : false;
            team = teamId;
            this.boardSize = boardSize;
            board = new Field[boardSize.Y, boardSize.X];
            for (int i = 0; i < boardSize.Y; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    board[i, j] = new Field();
                }
            }
            position = pos;
            teamMates = new int[alliesIds.Length];
            teamMates = alliesIds;
            goalAreaSize = goalAreaHeight;
            this.penalties = penalties;
            averageTime = penalties.Count > 0 ? (int)penalties.Values.Max().TotalMilliseconds : 500;
            this.shamPieceProbability = shamPieceProbability;
            logger.Info("Initialize: Agent initialized" + " AgentID: " + id.ToString());
        }

        private void SetPenalty(ActionType action)
        {
            var ret = penalties.TryGetValue(action, out TimeSpan span);
            if (ret) remainingPenalty += span.TotalSeconds;
        }

        public void SetDoNothingStrategy()
        {
            this.strategy = new DoNothingStrategy();
        }

        private int[,] GetDistances()
        {
            int[,] distances = new int[boardSize.Y, boardSize.X];
            for (int i = 0; i < boardSize.Y; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    distances[i, j] = board[i, j].distToPiece;
                }
            }
            return distances;
        }

        private GoalInformation[,] GetBlueTeamGoalAreaInformation()
        {
            GoalInformation[,] goalAreaInformation = new GoalInformation[goalAreaSize, boardSize.X];
            for (int i = 0; i < goalAreaSize; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    goalAreaInformation[i, j] = board[i, j].goalInfo;
                }
            }
            return goalAreaInformation;
        }

        private GoalInformation[,] GetRedTeamGoalAreaInformation()
        {
            GoalInformation[,] goalAreaInformation = new GoalInformation[goalAreaSize, boardSize.X];
            for (int i = boardSize.Y - goalAreaSize + 1; i < boardSize.Y; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    goalAreaInformation[i - boardSize.Y + goalAreaSize, j] = board[i, j].goalInfo;
                }
            }
            return goalAreaInformation;
        }

        private void UpdateDistances(int[,] distances)
        {
            //TODO: update only when distLearned old
            for (int i = 0; i < boardSize.Y; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    board[i, j].distToPiece = distances[i, j];
                }
            }
        }

        private void UpdateBlueTeamGoalAreaInformation(GoalInformation[,] goalAreaInformation)
        {
            for (int i = 0; i < goalAreaSize; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    if (board[i, j].goalInfo == GoalInformation.NoInformation) board[i, j].goalInfo = goalAreaInformation[i, j];
                }
            }
        }

        private void UpdateRedTeamGoalAreaInformation(GoalInformation[,] goalAreaInformation)
        {
            for (int i = boardSize.Y - goalAreaSize + 1; i < boardSize.Y; i++)
            {
                for (int j = 0; j < boardSize.X; j++)
                {
                    board[i, j].goalInfo = goalAreaInformation[i - boardSize.Y + goalAreaSize, j];
                }
            }
        }

        public ActionResult Update(double dt)
        {
            if (agentState == AgentState.Finished) return ActionResult.Finish;
            remainingPenalty = Math.Max(0.0, remainingPenalty - dt);
            if (remainingPenalty > 0.0) return ActionResult.Continue;
            switch (agentState)
            {
                case AgentState.Created:
                    SendMessage(MessageFactory.GetMessage(new JoinRequest(team, wantsToBeLeader)));
                    agentState = AgentState.WaitingForJoin;
                    return ActionResult.Continue;
                case AgentState.WaitingForJoin:
                    var joinResponse = GetMessage(MessageId.JoinResponse);
                    if (joinResponse == null) return ActionResult.Continue;
                    if (AcceptMessage(joinResponse) == ActionResult.Finish)
                    {
                        agentState = AgentState.Finished;
                        return ActionResult.Finish;
                    }
                    return ActionResult.Continue;
                case AgentState.WaitingForStart:
                    var startResponse = GetMessage(MessageId.StartGameMessage);
                    if (startResponse == null) return ActionResult.Continue;
                    if (AcceptMessage(startResponse) == ActionResult.Finish)
                    {
                        agentState = AgentState.Finished;
                        return ActionResult.Finish;
                    }
                    return ActionResult.Continue;
                case AgentState.InGame:
                    BaseMessage message = GetMessage();
                    if (message == null && skipCount < maxSkipCount)
                    {
                        skipCount++;
                        return ActionResult.Continue;
                    }
                    skipCount = 0;
                    ActionResult ret = message == null ? MakeDecisionFromStrategy() : AcceptMessage(message);
                    if (ret == ActionResult.Finish)
                    {
                        agentState = AgentState.Finished;
                        return ActionResult.Finish;
                    }
                    return ActionResult.Continue;
                default:
                    logger.Error("Agent in unknown state: " + agentState.ToString() + " AgentID: " + id.ToString());
                    return ActionResult.Finish;
            }
        }

        public ActionResult Move(Direction direction)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Move: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            lastDirection = direction;
            SetPenalty(ActionType.Move);
            SendMessage(MessageFactory.GetMessage(new MoveRequest(direction)));
            logger.Info("Move: Agent sent move request in direction " + direction.ToString() + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult PickUp()
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Pick up: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            SendMessage(MessageFactory.GetMessage(new PickUpPieceRequest()));
            logger.Info("Pick up: Agent sent pick up piece request." + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult Put()
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Put: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            SetPenalty(ActionType.PutPiece);
            SendMessage(MessageFactory.GetMessage(new PutDownPieceRequest()));
            logger.Info("Put: Agent sent put down piece request." + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult BegForInfo()
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Beg for info: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
                return MakeDecisionFromStrategy();
            }
            if (teamMates.Length == 0)
            {
                logger.Warn("Beg for info: Agent does not know his teammates" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
                return MakeDecisionFromStrategy();
            }
            lastAskedTeammate++;
            lastAskedTeammate %= teamMates.Length;
            SetPenalty(ActionType.InformationRequest);
            SendMessage(MessageFactory.GetMessage(new ExchangeInformationRequest(teamMates[lastAskedTeammate])));
            logger.Info("Beg for info: Agent sent exchange information request." + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult GiveInfo(int respondToId = -1)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Give info: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            if (respondToId == -1 && waitingPlayers.Count > 0)
            {
                respondToId = waitingPlayers[0];
                waitingPlayers.RemoveAt(0);
                logger.Info("Give info: ResponfdId is -1. Respond to first waiting player." + " AgentID: " + id.ToString());
            }
            if (respondToId == -1)
            {
                logger.Warn("Give info: Respond to id -1 while give info" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            else if (respondToId == -1) return MakeDecisionFromStrategy();
            SetPenalty(ActionType.InformationResponse);
            SendMessage(MessageFactory.GetMessage(new ExchangeInformationResponse(respondToId, GetDistances(), GetRedTeamGoalAreaInformation(), GetBlueTeamGoalAreaInformation())));
            logger.Info("Give info: Agent sent exchange information response to adentId: " + respondToId.ToString() + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult CheckPiece()
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Check piece: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            SetPenalty(ActionType.CheckForSham);
            SendMessage(MessageFactory.GetMessage(new CheckShamRequest()));
            logger.Info("Check piece: Agent sent check scham request." + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult Discover()
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Discover: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            SetPenalty(ActionType.Discovery);
            SendMessage(MessageFactory.GetMessage(new DiscoverRequest()));
            logger.Info("Discover: Agent sent discover request." + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult DestroyPiece()
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Destroy Piece: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedAction) return ActionResult.Finish;
            }
            SetPenalty(ActionType.DestroyPiece);
            SendMessage(MessageFactory.GetMessage(new DestroyPieceRequest()));
            logger.Info("Destroy Piece: Agent sent destroy piece request." + " AgentID: " + id.ToString());
            return ActionResult.Continue;
        }

        public ActionResult MakeDecisionFromStrategy()
        {
            return strategy.MakeDecision(this);
        }

        private BaseMessage GetMessage()
        {
            if (injectedMessages.Count == 0)
            {
                return null;
            }
            var message = injectedMessages.FirstOrDefault(m => m.PayloadType == typeof(EndGamePayload));
            if (message == null) message = injectedMessages[0];
            injectedMessages.Remove(message);
            return message;
        }

        private BaseMessage GetMessage(MessageId messageId)
        {
            var message = injectedMessages.FirstOrDefault(m => m.MessageId == messageId);
            if (message == null) message = injectedMessages.FirstOrDefault(m => m.MessageId == messageId);
            if (message != null) injectedMessages.Remove(message);
            return message;
        }

        public void InjectMessage(BaseMessage message)
        {
            injectedMessages.Add(message);
        }

        public void SendMessage(BaseMessage message)
        {
            MockMessageSendFunction?.Invoke(this, message);
        }

        public ActionResult AcceptMessage(BaseMessage message)
        {
            dynamic dynamicMessage = message;
            return Process(dynamicMessage);
        }

        private ActionResult Process(Message<CheckShamResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process check scham response: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (message.Payload.Sham)
            {
                logger.Info("Process check scham response: Agent checked sham and destroy piece." + " AgentID: " + id.ToString());
                return DestroyPiece();
            }
            else
            {
                logger.Info("Process check scham response: Agent checked not sham." + " AgentID: " + id.ToString());
                piece.isDiscovered = true;
                return MakeDecisionFromStrategy();
            }
        }

        private ActionResult Process(Message<DestroyPieceResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process destroy piece response: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            piece = null;
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<DiscoverResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process discover response: Agent not in game." + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            DateTime now = DateTime.Now;
            for (int y = position.Y - 1; y <= position.Y + 1; y++)
            {
                for (int x = position.X - 1; x <= position.X + 1; x++)
                {
                    int taby = y - position.Y + 1;
                    int tabx = x - position.X + 1;
                    if (Common.OnBoard(new Point(x, y), boardSize))
                    {
                        board[y, x].distToPiece = message.Payload.Distances[taby, tabx];
                        board[y, x].distLearned = now;
                    }
                }
            }
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<ExchangeInformationResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process exchange information response: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            UpdateDistances(message.Payload.Distances);
            UpdateBlueTeamGoalAreaInformation(message.Payload.BlueTeamGoalAreaInformation);
            UpdateRedTeamGoalAreaInformation(message.Payload.RedTeamGoalAreaInformation);
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<MoveResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process move response: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            position = message.Payload.CurrentPosition;
            if (message.Payload.MadeMove)
            {
                deniedLastMove = false;
                board[position.Y, position.X].distToPiece = message.Payload.ClosestPoint;
                board[position.Y, position.X].distLearned = DateTime.Now;
                if (message.Payload.ClosestPoint == 0/* && board[position.Y, position.X].goalInfo == GoalInformation.NoInformation*/)
                {
                    logger.Info("Process move response: agent pick up piece." + " AgentID: " + id.ToString());
                    return PickUp();
                }
            }
            else
            {
                deniedLastMove = true;
                logger.Info("Process move response: agent did not move." + " AgentID: " + id.ToString());
                var deniedField = Common.GetFieldInDirection(position, lastDirection);
                if (Common.OnBoard(deniedField, boardSize)) board[deniedField.Y, deniedField.X].deniedMove = DateTime.Now;
            }
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<PickUpPieceResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process pick up piece response: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (board[position.Y, position.X].distToPiece == 0)
            {
                logger.Info("Process pick up piece response: Agent picked up piece" + " AgentID: " + id.ToString());
                piece = new Piece();
            }
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<PutDownPieceResponse> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process put down piece response: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            piece = null;
            switch (message.Payload.Result)
            {
                case PutDownPieceResult.NormalOnGoalField:
                    board[position.Y, position.X].goalInfo = GoalInformation.Goal;
                    break;
                case PutDownPieceResult.NormalOnNonGoalField:
                    board[position.Y, position.X].goalInfo = GoalInformation.NoGoal;
                    break;
                case PutDownPieceResult.ShamOnGoalArea:
                    break;
                case PutDownPieceResult.TaskField:
                    board[position.Y, position.X].goalInfo = GoalInformation.NoGoal;
                    board[position.Y, position.X].distToPiece = 0;
                    board[position.Y, position.X].distLearned = DateTime.Now;
                    break;
            }
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<ExchangeInformationPayload> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process exchange information payload: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (message.Payload.Leader)
            {
                logger.Info("Process exchange information payload: Agent give info to leader" + " AgentID: " + id.ToString());
                return GiveInfo(message.Payload.AskingAgentId);
            }
            else
            {
                waitingPlayers.Add(message.Payload.AskingAgentId);
                return MakeDecisionFromStrategy();
            }
        }

        private ActionResult Process(Message<JoinResponse> message)
        {
            if (agentState != AgentState.WaitingForJoin)
            {
                logger.Warn("Process join response: Agent not waiting for join" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            if (message.Payload.Accepted)
            {
                bool wasWaiting = agentState == AgentState.WaitingForJoin;
                agentState = AgentState.WaitingForStart;
                id = message.Payload.AgentId;
                return wasWaiting ? ActionResult.Continue : MakeDecisionFromStrategy();
            }
            else
            {
                logger.Info("Process join response: Join request not accepted" + " AgentID: " + id.ToString());
                return ActionResult.Finish;
            }
        }

        private ActionResult Process(Message<StartGamePayload> message)
        {
            if (agentState != AgentState.WaitingForStart)
            {
                logger.Warn("Process start game payload: Agent not waiting for startjoin" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            Initialize(message.Payload.LeaderId, message.Payload.TeamId, message.Payload.BoardSize, message.Payload.GoalAreaHeight, message.Payload.Position, message.Payload.AlliesIds, message.Payload.Penalties, message.Payload.ShamPieceProbability);
            if (id != message.Payload.AgentId)
            {
                logger.Warn("Process start game payload: payload.agnetId not equal agentId" + " AgentID: " + id.ToString());
            }
            agentState = AgentState.InGame;
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<EndGamePayload> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process end game payload: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Info("Process End Game: end game" + " AgentID: " + id.ToString());
            return ActionResult.Finish;
        }

        private ActionResult Process(Message<IgnoredDelayError> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process ignoreed delay error: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("IgnoredDelay error" + " AgentID: " + id.ToString());
            var time = message.Payload.RemainingDelay;
            remainingPenalty = Math.Max(0.0, time.TotalSeconds);
            return ActionResult.Continue;
        }

        private ActionResult Process(Message<MoveError> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process move error: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("Move error" + " AgentID: " + id.ToString());
            deniedLastMove = true;
            position = message.Payload.Position;
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<PickUpPieceError> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process pick up piece error: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("Pick up piece error" + " AgentID: " + id.ToString());
            board[position.Y, position.X].distLearned = DateTime.Now;
            board[position.Y, position.X].distToPiece = int.MaxValue;
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<PutDownPieceError> message)
        {
            if (agentState != AgentState.InGame)
            {
                logger.Warn("Process put down piece error: Agent not in game" + " AgentID: " + id.ToString());
                if (endIfUnexpectedMessage) return ActionResult.Finish;
            }
            logger.Warn("Put down piece error" + " AgentID: " + id.ToString());
            if (message.Payload.ErrorSubtype == PutDownPieceErrorSubtype.AgentNotHolding) piece = null;
            return MakeDecisionFromStrategy();
        }

        private ActionResult Process(Message<UndefinedError> message)
        {
            logger.Warn("Undefined error" + " AgentID: " + id.ToString());
            return MakeDecisionFromStrategy();
        }
    }
}
