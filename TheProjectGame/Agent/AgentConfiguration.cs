﻿using Messaging.Enumerators;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Agent
{
    public class AgentConfiguration
    {
        public string CsIP { get; set; }
        public string CsPort { get; set; }
        public string teamID { get; set; }
        public int strategy { get; set; }

        public AgentConfiguration GetConfiguration()
        {
            string fileName = "Configuration\\agentConfiguration.json";
            AgentConfiguration agentConfiguration = JsonConvert.DeserializeObject<AgentConfiguration>(File.ReadAllText(@fileName));
            return agentConfiguration;
        }
    }
}