﻿using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using Messaging.Contracts;

namespace GameMaster
{
    public class GameMaster
    {
        public BoardLogicComponent BoardLogic { get; private set; }

        private GameMasterState state = GameMasterState.Configuration;
        private List<Agent> agents = new List<Agent>();

        public GameMaster()
        {
            LoadDefaultConfiguration();

            //create board with deafult parameters
            BoardLogic = new BoardLogicComponent(new Point(5, 10));

            //try to connect to communciation server
        }

        public void SetNetworkConfiguration(/*network configuration*/) { }
        public void SetBoardConfiguration(/*board configuration*/) { }
        public void SetAgentsConfiguartion(/*agents configuration*/) { }

        public void ApplyConfiguration()
        {
            //if ok start accepting agents
            state = GameMasterState.ConnectingAgents;
        }

        public void StartGame()
        {
            state = GameMasterState.InGame;
        }

        public void PauseGame()
        {
            state = GameMasterState.Paused;
        }

        //called from window system each frame, updates all components
        public void Update(double dt)
        {
            foreach (var agent in agents)
                agent.Update(dt);

            var messages = GetIncomingMessages();
        }

        public Agent GetAgent(int agentId)
        {
            return agents.FirstOrDefault(a => a.Id == agentId);
        }

        //TODO: move to messaging system
        private List<BaseMessage> GetIncomingMessages()
        {
            return new List<BaseMessage>();
        }

        private void LoadDefaultConfiguration()
        {
            
        }
    }
}
