﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Agent.strategies
{
    class SimpleStrategy : IStrategy
    {
        private const int shortPieceDistance = 4;

        private const int shortTime = 4;

        private const int smallUndiscoveredNumber = 4;

        private int stayInLineCount = 0;

        private Point GetFieldInDirection(Point position, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return new Point(position.X, position.Y + 1);
                case Direction.Right:
                    return new Point(position.X + 1, position.Y);
                case Direction.Down:
                    return new Point(position.X, position.Y - 1);
                case Direction.Left:
                    return new Point(position.X - 1, position.Y);
            }
            return position;
        }

        private bool InGoalArea(Team team, Point position, Point boardSize, int goalAreaSize)
        {
            return team == Team.Red ? position.X >= boardSize.X - goalAreaSize : position.X < goalAreaSize;
        }

        private bool OnBoard(Point position, Point boardSize)
        {
            return position.X >= 0 && position.Y >= 0 && position.X < boardSize.X && position.Y < boardSize.Y;
        }

        private bool CouldMove(Agent agent, Direction direction)
        {
            Point target = GetFieldInDirection(agent.position, direction);
            return OnBoard(target, agent.boardSize) &&
                DateTime.Now - agent.board[target.X, target.Y].deniedMove > shortTime * TimeSpan.FromMilliseconds(agent.penaltyTime);
        }

        private Direction GetGoalDirection(Agent agent)
        {
            if (agent.team == Team.Red)
            {
                if (CouldMove(agent, Direction.Right)) return Direction.Right;
                if (CouldMove(agent, Direction.Up)) return Direction.Up;
                if (CouldMove(agent, Direction.Down)) return Direction.Down;
                return Direction.Right;
            }
            else
            {
                if (CouldMove(agent, Direction.Left)) return Direction.Left;
                if (CouldMove(agent, Direction.Up)) return Direction.Up;
                if (CouldMove(agent, Direction.Down)) return Direction.Down;
                return Direction.Left;
            }
        }

        private Direction StayInGoalArea(Agent agent)
        {
            Direction direction;
            if (stayInLineCount > agent.boardSize.Y) direction = GetGoalDirection(agent);
            else if (CouldMove(agent, Direction.Up)) direction = Direction.Up;
            else if (CouldMove(agent, Direction.Down)) direction = Direction.Down;
            else direction = GetGoalDirection(agent);
            if (direction == Direction.Up || direction == Direction.Down) stayInLineCount++;
            else stayInLineCount = 0;
            return direction;
        }

        private int FindClosest(Agent agent, out Direction direction)
        {
            int shortest = int.MaxValue;
            direction = Direction.Up;
            for (int i = agent.position.X - 1; i <= agent.position.X + 1; i++)
                for (int j = agent.position.Y - 1; j <= agent.position.Y + 1; j++)
                    if ((i != agent.position.X || j != agent.position.Y) &&
                        OnBoard(new Point(i, j), agent.boardSize) &&
                        DateTime.Now - agent.board[i, j].distLearned > TimeSpan.FromMilliseconds(shortTime * agent.penaltyTime) &&
                        agent.board[i, j].distToPiece < Math.Min(shortest, agent.board[agent.position.X, agent.position.Y].distToPiece))
                    {
                        shortest = agent.board[i, j].distToPiece;
                        if (j > agent.position.Y) direction = Direction.Up;
                        else if (j < agent.position.Y) direction = Direction.Down;
                        else if (i < agent.position.X) direction = Direction.Left;
                        else direction = Direction.Right;
                    }
            return shortest;
        }

        private int CountUndiscoveredFields(Agent agent)
        {
            int count = 0;
            for (int i = agent.position.X - 1; i <= agent.position.X + 1; i++)
                for (int j = agent.position.Y - 1; j <= agent.position.Y + 1; j++)
                    if (OnBoard(new Point(i, j), agent.boardSize) &&
                        DateTime.Now - agent.board[i, j].distLearned > TimeSpan.FromMilliseconds(shortTime * agent.penaltyTime))
                        count++;
            return count;
        }

        public void MakeDecision(Agent agent)
        {
            if (!InGoalArea(agent.team, agent.position, agent.boardSize, agent.goalAreaSize)) stayInLineCount = 0;
            if (agent.waitingPlayers.Count > 0)
            {
                agent.GiveInfo();
                return;
            }
            if (agent.piece != null && !agent.piece.isDiscovered)
            {
                agent.CheckPiece();
                return;
            }
            if (agent.piece != null && agent.piece.isDiscovered &&
                agent.board[agent.position.X, agent.position.Y].goalInfo == GoalInfo.IDK &&
                InGoalArea(agent.team, agent.position, agent.boardSize, agent.goalAreaSize))
            {
                agent.Put();
                stayInLineCount = 0;
                return;
            }
            if (agent.piece != null && agent.piece.isDiscovered &&
                InGoalArea(agent.team, agent.position, agent.boardSize, agent.goalAreaSize))
            {
                agent.Move(StayInGoalArea(agent));
                return;
            }
            if (agent.piece != null && agent.piece.isDiscovered)
            {
                agent.Move(GetGoalDirection(agent));
                return;
            }
            if (FindClosest(agent, out Direction direction) <= shortPieceDistance)
            {
                agent.Move(direction);
                return;
            }
            if (CountUndiscoveredFields(agent) > smallUndiscoveredNumber)
            {
                agent.Discover();
                return;
            }
            agent.BegForInfo();
        }
    }
}
