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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Collects all GameMode traits and enables choosing between them in the lobby.",
		"Use this to enable different game modes on the same map. Attach this to the Player actor.")]
	class GameModeManagerInfo : TraitInfo, ILobbyOptions, Requires<GameModeInfo>
	{
		public enum DropdownVisibility { Auto, Shown, Hidden }

		[Translate]
		[Desc("Descriptive label for the game mode option in the lobby.")]
		public readonly string GameModeLabel = "Game Mode";

		[Translate]
		[Desc("Tooltip description for the game mode option in the lobby.")]
		public readonly string GameModeDescription = "Select the game mode";

		[Desc("Prevent the game mode option from being changed in the lobby.")]
		public readonly bool GameModeLocked = false;

		[Desc("Whether to display the game mode option in the lobby.",
			"Setting this to 'Auto' will hide the dropdown if only one option is available.")]
		public readonly DropdownVisibility GameModeVisible = DropdownVisibility.Auto;

		[Desc("Display order for the game mode option in the lobby.")]
		public readonly int GameModeDisplayOrder = 0;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(Ruleset rules)
		{
			var modes = rules.Actors["player"].TraitInfos<GameModeInfo>().Concat(rules.Actors["world"].TraitInfos<GameModeInfo>())
				.Select(m => new KeyValuePair<string, string>(m.InternalName, m.Name)).ToDictionary(x => x.Key, x => x.Value);
			var dropdownVisible = GameModeVisible == DropdownVisibility.Shown || (GameModeVisible == DropdownVisibility.Auto && modes.Count > 1);
			var defaultValue = modes.Count > 0 ? modes.First().Key : "none";

			yield return new LobbyOption("gamemode", GameModeLabel, GameModeDescription, dropdownVisible, GameModeDisplayOrder,
				new ReadOnlyDictionary<string, string>(modes), defaultValue, GameModeLocked);
		}

		public override object Create(ActorInitializer init) { return new GameModeManager(init.Self, this); }
	}

	class GameModeManager
	{
		public readonly GameMode ActiveGameMode;

		public GameModeManager(Actor self, GameModeManagerInfo info)
		{
			var mode = self.World.LobbyInfo.GlobalSettings.OptionOrDefault("gamemode", "shellmap");
			ActiveGameMode = self.TraitsImplementing<GameMode>().Concat(self.World.WorldActor.TraitsImplementing<GameMode>())
				.Where(m => m.Info.InternalName == mode).First();

			if (ActiveGameMode.Info.Condition != null)
			{
				self.GrantCondition(ActiveGameMode.Info.Condition);
				self.World.WorldActor.GrantCondition(ActiveGameMode.Info.Condition);
			}
		}
	}
}
