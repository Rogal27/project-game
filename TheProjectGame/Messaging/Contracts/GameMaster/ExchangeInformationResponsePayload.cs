﻿using Messaging.Contracts.Agent;
using Messaging.Enumerators;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Messaging.Contracts.GameMaster
{
    public class ExchangeInformationResponsePayload : IPayload
    {
        public MessageId GetMessageId() => MessageId.ExchangeInformationResponseMessage;
        
        [JsonRequired]
        [JsonProperty(PropertyName = "respondingID")]
        public int RespondingId { get; private set; }

        // TODO: Make sure how to format those arrays into one-dimensional
        [JsonRequired]
        [JsonProperty(PropertyName = "distances")]
        public int[,] Distances { get; private set; }

        [JsonRequired]
        [JsonProperty(PropertyName = "redTeamGoalAreaInformations")]
        public GoalInformation[,] RedTeamGoalAreaInformation { get; private set; }

        [JsonRequired]
        [JsonProperty(PropertyName = "blueTeamGoalAreaInformations")]
        public GoalInformation[,] BlueTeamGoalAreaInformation { get; private set; }

        [JsonConstructor]
        public ExchangeInformationResponsePayload(int respondingId, int[,] distances, GoalInformation[,] redTeamGoalAreaInformation, GoalInformation[,] blueTeamGoalAreaInformation)
        {
            RespondingId = respondingId;
            Distances = distances;
            RedTeamGoalAreaInformation = redTeamGoalAreaInformation;
            BlueTeamGoalAreaInformation = blueTeamGoalAreaInformation;
        }

        public ExchangeInformationResponsePayload(ExchangeInformationResponse message) : this (
            message.RespondToId,
            message.Distances,
            message.RedTeamGoalAreaInformation,
            message.BlueTeamGoalAreaInformation
        ) { }
    }
}
