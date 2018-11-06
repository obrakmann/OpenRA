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

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.UpdateRules.Rules
{
	public class PlayerExperiencePerGameMode : UpdateRule
	{
		public override string Name { get { return "Change PlayerExperience field to a Dictionary across several traits."; } }
		public override string Description
		{
			get
			{
				return "The PlayerExperience field of several traits was changed from a single\n"
					+ "value to a Dictionary keyed on the game mode in use.";
			}
		}

		readonly string[] modifiedTraits =
		{
			"Captures", "DeliversCash", "DeliversExperience", "GivesExperience", "Infiltrates",
			"RepairableBuilding", "RepairsUnits",
		};

		public override IEnumerable<string> UpdateActorNode(ModData modData, MiniYamlNode actorNode)
		{
			foreach (var trait in modifiedTraits.SelectMany(t => actorNode.ChildrenMatching(t)))
			{
				var pe = trait.LastChildMatching("PlayerExperience");
				if (pe != null)
				{
					var val = pe.Value.Value;
					pe.ReplaceValue("");
					pe.AddNode("conquest", val);
					pe.AddNode("koth", val);
				}
			}

			yield break;
		}
	}
}
