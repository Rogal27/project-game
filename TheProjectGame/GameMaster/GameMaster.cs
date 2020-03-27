﻿using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using Messaging.Contracts;
using GameMaster.Interfaces;
using Messaging.Enumerators;

namespace GameMaster
{
    public class GameMaster
    {
        public BoardLogicComponent BoardLogic { get; private set; }
        public ConnectionLogicComponent ConnectionLogic { get; private set; }
        public GameLogicComponent GameLogic { get; private set; }
        public List<Agent> Agents { get; private set; } = new List<Agent>();

        public ScoreComponent ScoreComponent { get; private set; }
        public GameMasterConfiguration Configuration { get; private set; }

        private GameMasterState state = GameMasterState.Configuration;
        private IMessageProcessor currentMessageProcessor = null;

        public GameMaster()
        {
            LoadDefaultConfiguration();

            ConnectionLogic = new ConnectionLogicComponent(this);
            GameLogic = new GameLogicComponent(this);
            ScoreComponent = new ScoreComponent(this);
            BoardLogic = new BoardLogicComponent(this, new Point(Configuration.BoardX, Configuration.BoardY));

            //try to connect to communciation server
        }

        public void SetNetworkConfiguration(/*network configuration*/) { }
        public void SetBoardConfiguration(/*board configuration*/) { }
        public void SetAgentsConfiguartion(/*agents configuration*/) { }

        public void ApplyConfiguration()
        {
            //if ok start accepting agents
            state = GameMasterState.ConnectingAgents;
            currentMessageProcessor = ConnectionLogic;
        }

        public void StartGame()
        {
            Agents = ConnectionLogic.FlushLobby();
            state = GameMasterState.InGame;
            currentMessageProcessor = GameLogic;
            BoardLogic.GenerateGoals();

            //TODO: send
            GameLogic.GetStartGameMessages();
        }

        public void PauseGame()
        {
            state = GameMasterState.Paused;

            //TODO: send
            GameLogic.GetPauseMessages();
        }

        public void ResumeGame()
        {
            state = GameMasterState.InGame;

            //TODO: send
            GameLogic.GetResumeMessages();
        }

        //called from window system each frame, updates all components
        public void Update(double dt)
        {
            if (state == GameMasterState.Configuration || state == GameMasterState.Summary)
                return;

            if (state == GameMasterState.InGame)
                BoardLogic.Update(dt);
            
            foreach (var agent in Agents)
                agent.Update(dt);

            var messages = GetIncomingMessages();
            foreach (var message in messages)
            {
                var response = currentMessageProcessor.ProcessMessage(message);
                //TODO: send response
            }

            var result = ScoreComponent.GetGameResult();
            if (result != Enums.GameResult.None)
            {
                state = GameMasterState.Summary;

                //TODO: send
                GameLogic.GetEndGameMessages(result == Enums.GameResult.BlueWin ? TeamId.Blue : TeamId.Red);
            }
        }

        public Agent GetAgent(int agentId)
        {
            return Agents.FirstOrDefault(a => a.Id == agentId);
        }

        //TODO: move to messaging system
#if DEBUG
        private List<BaseMessage> injectedMessages = new List<BaseMessage>();

        public void InjectMessage(BaseMessage message)
        {
            injectedMessages.Add(message);
        }
#endif

        private List<BaseMessage> GetIncomingMessages()
        {
#if DEBUG
            var clone = new List<BaseMessage>(injectedMessages);
            injectedMessages.Clear();
            return clone;
#endif
            return new List<BaseMessage>();
        }

        private void LoadDefaultConfiguration()
        {
            var configurationProvider = new MockConfigurationProvider();
            Configuration = configurationProvider.GetConfiguration();
        }
    }
}