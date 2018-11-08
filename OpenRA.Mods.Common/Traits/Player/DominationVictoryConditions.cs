#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("VictoryConditions trait for the Domination game mode, which is a variant of King of the Hill.")]
	class DominationVictoryConditionsInfo : ConditionalTraitInfo, Requires<MissionObjectivesInfo>, Requires<PlayerExperienceInfo>
	{
		[Translate]
		[Desc("Description of the objective. If the text includes the string \"{0}\", it will be replaced by the score limit",
		      "selected in the lobby.")]
		public readonly string Objective = "Reach a score of {0} by capturing strategic points on the map!";

		[Desc("The number of points each captured strategic point adds to the total score.")]
		public readonly int PointsPerCapture = 1;

		[Desc("The time interval at which points accumulate, in ticks.")]
		public readonly int ScoreInterval = 25;

		[Desc("Delay for the end game notification in milliseconds.")]
		public readonly int NotificationDelay = 1500;

		[Desc("Disable the win/loss messages and audio notifications?")]
		public readonly bool SuppressNotifications = false;

		public override object Create(ActorInitializer init) { return new DominationVictoryConditions(init.Self, this); }
	}

	class DominationVictoryConditions : ConditionalTrait<DominationVictoryConditionsInfo>, ITick, INotifyWinStateChanged, INotifyTimeLimit
	{
		readonly DominationVictoryConditionsInfo info;
		readonly Player player;
		readonly MissionObjectives objectives;
		readonly PlayerExperience experience;
		readonly bool shortGame;
		readonly int scoreLimit;

		int objectiveID = -1;
		Dictionary<Player, PlayerExperience> alliesExperience;
		Player[] otherPlayers;
		bool initialized;

		public DominationVictoryConditions(Actor self, DominationVictoryConditionsInfo info)
			: base(info)
		{
			this.info = info;
			player = self.Owner;
			objectives = self.Trait<MissionObjectives>();
			experience = self.Trait<PlayerExperience>();
			shortGame = player.World.WorldActor.Trait<MapOptions>().ShortGame;
			scoreLimit = int.Parse(self.World.LobbyInfo.GlobalSettings.OptionOrDefault("scorelimit", "1"));
		}

		public IEnumerable<Actor> AllPoints
		{
			get { return player.World.ActorsHavingTrait<StrategicPoint>(); }
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled)
				return;

			if (player.WinState != WinState.Undefined || player.NonCombatant)
				return;

			if (objectiveID < 0)
				objectiveID = objectives.Add(self.Owner, info.Objective.F(scoreLimit), "Primary", inhibitAnnouncement: true);

			// Players, NonCombatants, and IsAlliedWith are all fixed once the game starts, so we can cache the result.
			if (alliesExperience == null)
			{
				alliesExperience = new Dictionary<Player, PlayerExperience>();
				var allies = self.World.Players.Where(p => !p.NonCombatant && p.IsAlliedWith(player));
				foreach (var p in allies)
					alliesExperience.Add(p, p.PlayerActor.Trait<PlayerExperience>());
			}

			if (otherPlayers == null)
				otherPlayers = self.World.Players.Where(p => !p.NonCombatant && !p.IsAlliedWith(player)).ToArray();

			if (self.World.WorldTick % info.ScoreInterval == 0)
			{
				var pointsThisTick = AllPoints.Count(a => a.Owner == player) * info.PointsPerCapture;
				experience.GiveExperience(pointsThisTick);

				var accumulatedScore = alliesExperience.Sum(kv => kv.Value.Experience);

				if (accumulatedScore >= scoreLimit)
					objectives.MarkCompleted(player, objectiveID);
			}

			if (player.HasNoRequiredUnits(shortGame))
				objectives.MarkFailed(player, objectiveID);

			// Prevent the game from ending at once when only one player is on the map
			if (otherPlayers.Length == 0)
				return;

			// Check if any enemy player has completed their objectives. If so, lose immediately.
			foreach (var otherPlayer in otherPlayers)
				if (otherPlayer.WinState == WinState.Won)
					objectives.MarkFailed(player, objectiveID);

			// Check if any enemy player is still in the game. If so, stop processing.
			foreach (var otherPlayer in otherPlayers)
				if (otherPlayer.WinState != WinState.Lost)
					return;

			// Last man standing. Looks like we won by default.
			objectives.MarkCompleted(player, objectiveID);
		}

		void INotifyTimeLimit.NotifyTimerExpired(Actor self)
		{
			if (objectiveID < 0)
				return;

			var myTeam = self.World.LobbyInfo.ClientWithIndex(self.Owner.ClientIndex).Team;
			var teams = self.World.Players.Where(p => !p.NonCombatant && p.Playable)
				.Select(p => new Pair<Player, PlayerStatistics>(p, p.PlayerActor.TraitOrDefault<PlayerStatistics>()))
				.OrderByDescending(p => p.Second != null ? p.Second.Experience : 0)
				.GroupBy(p => (self.World.LobbyInfo.ClientWithIndex(p.First.ClientIndex) ?? new Session.Client()).Team)
				.OrderByDescending(g => g.Sum(gg => gg.Second != null ? gg.Second.Experience : 0));

			if (teams.First().Key == myTeam)
			{
				if (myTeam != 0 || teams.First().First().First == self.Owner)
				{
					objectives.MarkCompleted(self.Owner, objectiveID);
					return;
				}
			}

			objectives.MarkFailed(self.Owner, objectiveID);
		}

		void INotifyWinStateChanged.OnPlayerLost(Player loser)
		{
			if (IsTraitDisabled)
				return;

			foreach (var a in player.World.ActorsWithTrait<INotifyOwnerLost>().Where(a => a.Actor.Owner == player))
				a.Trait.OnOwnerLost(a.Actor);

			if (info.SuppressNotifications)
				return;

			Game.AddSystemLine("Battlefield Control", player.PlayerName + " is defeated.");
			Game.RunAfterDelay(info.NotificationDelay, () =>
			{
				if (Game.IsCurrentWorld(player.World) && player == player.World.LocalPlayer)
					Game.Sound.PlayNotification(player.World.Map.Rules, player, "Speech", objectives.Info.LoseNotification, player.Faction.InternalName);
			});
		}

		void INotifyWinStateChanged.OnPlayerWon(Player winner)
		{
			if (IsTraitDisabled)
				return;

			foreach (var p in otherPlayers)
				p.PlayerActor.Trait<MissionObjectives>().ForceDefeat(p);

			if (info.SuppressNotifications)
				return;

			Game.AddSystemLine("Battlefield Control", player.PlayerName + " is victorious.");
			Game.RunAfterDelay(info.NotificationDelay, () =>
			{
				if (Game.IsCurrentWorld(player.World) && player == player.World.LocalPlayer)
					Game.Sound.PlayNotification(player.World.Map.Rules, player, "Speech", objectives.Info.WinNotification, player.Faction.InternalName);
			});
		}

		protected override void TraitEnabled(Actor self)
		{
			if (initialized)
				throw new InvalidOperationException("Enabling another victory conditions trait mid-game is not supported.");
			initialized = true;
		}

		protected override void TraitDisabled(Actor self)
		{
			if (initialized)
				throw new InvalidOperationException("Disabling a victory conditions trait mid-game is not supported.");
			initialized = true;
		}
	}
}
