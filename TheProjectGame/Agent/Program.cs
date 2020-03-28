﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent
{
    class Program
    {
        public AgentConfiguration Configuration { get; private set; }
        public static Agent agent { get; set; }
        private int numberOfAgents { get; set; }
        static void Main(string[] args)
        {
            CreateAgent();
            AgentWork();
        }

        private static AgentConfiguration LoadDefaultConfiguration()
        {
            AgentConfiguration agentConfiguration = new AgentConfiguration();
            return agentConfiguration.GetConfiguration();
        }
     
        public static void CreateAgent()
        {
            agent = new Agent();
            AgentConfiguration agentConfiguration = LoadDefaultConfiguration();
            agent.CsIP = agentConfiguration.CsIP;
            agent.CsPort = agentConfiguration.CsPort;
            agent.JoinTheGame();
        }

        private static void AgentWork()
        {
            var message = agent.GetIncommingMessage();
            agent.AcceptMessage(message);
        }
    }
}
