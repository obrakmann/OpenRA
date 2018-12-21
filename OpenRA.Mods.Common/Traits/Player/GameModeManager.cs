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
	class GameModeManagerInfo : TraitInfo, ILobbyOptions, IEditorActorOptions, Requires<GameModeInfo>
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

		[Desc("Display order for the game mode option in the editor.")]
		public readonly int GameModeEditorDisplayOrder = 0;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(Ruleset rules)
		{
			var modes = rules.Actors["player"].TraitInfos<GameModeInfo>().Concat(rules.Actors["world"].TraitInfos<GameModeInfo>())
				.Select(m => new KeyValuePair<string, string>(m.InternalName, m.Name)).ToDictionary(x => x.Key, x => x.Value);
			var dropdownVisible = GameModeVisible == DropdownVisibility.Shown || (GameModeVisible == DropdownVisibility.Auto && modes.Count > 1);
			var defaultValue = modes.Count > 0 ? modes.First().Key : "none";

			yield return new LobbyOption("gamemode", GameModeLabel, GameModeDescription, dropdownVisible, GameModeDisplayOrder,
				new ReadOnlyDictionary<string, string>(modes), defaultValue, GameModeLocked);
		}

		IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, World world)
		{
			var modes = world.Map.Rules.Actors["player"].TraitInfos<GameModeInfo>().Concat(world.Map.Rules.Actors["world"].TraitInfos<GameModeInfo>())
				.Select(m => new KeyValuePair<string, string>(m.InternalName, m.Name)).ToDictionary(x => x.Key, x => x.Value);
			modes.Add("", "All modes");

			yield return new EditorActorDropdown("Game Mode", GameModeEditorDisplayOrder, modes,
			(actor) =>
			{
				// TODO: This only allows single selection, while manual editing would allow multiple selections.
				var init = actor.GetInitOrDefault<GameModesInit>();
				var mode = init != null ? init.Value.First() : "";
				return mode;
			},
			(actor, value) =>
			{
				if (value == "")
					actor.RemoveInit<GameModesInit>();
				else
					actor.ReplaceInit<GameModesInit>(new GameModesInit(new string[] { value }));
			});
		}

		public override object Create(ActorInitializer init) { return new GameModeManager(init.Self, this); }
	}

	class GameModeManager : IPreventMapSpawn
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

		bool IPreventMapSpawn.PreventMapSpawn(World world, ActorReference actorReference)
		{
			string[] gameModes = { };
			if (actorReference.Contains<GameModesInit>())
				gameModes = actorReference.Get<GameModesInit>().Value;

			if (gameModes.Any() && !gameModes.Contains(world.LobbyInfo.GlobalSettings.OptionOrDefault("gamemode", "")))
				return true;

			return false;
		}
	}

	// GameModeInit is used to restrict Actors to certain game modes, eg. only showing KotH flags on the koth game mode.
	// An empty value is interpreted to mean that the actor should appear in all game modes.
	// Matches against the InternalName field of a GameMode.
	public class GameModesInit : ValueActorInit<string[]>, ISingleInstanceInit
	{
		public GameModesInit(string[] value)
			: base(value) { }
	}
}
