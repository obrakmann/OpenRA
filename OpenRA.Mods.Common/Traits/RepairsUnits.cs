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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class RepairsUnitsInfo : PausableConditionalTraitInfo
	{
		[Desc("Cost in % of the unit value to fully repair the unit.")]
		public readonly int ValuePercentage = 20;

		public readonly int HpPerStep = 10;

		[Desc("Time (in ticks) between two repair steps.")]
		public readonly int Interval = 24;

		[Desc("Damage types used for the repair.")]
		public readonly BitSet<DamageType> RepairDamageTypes = default(BitSet<DamageType>);

		[NotificationReference("Speech")]
		[Desc("The sound played when starting to repair a unit.")]
		public readonly string StartRepairingNotification = null;

		[NotificationReference("Speech")]
		[Desc("The sound played when repairing a unit is done.")]
		public readonly string FinishRepairingNotification = null;

		[Desc("Experience gained by the player owning this actor for repairing an allied unit, by game mode.")]
		public readonly Dictionary<string, int> PlayerExperience = new Dictionary<string, int>();

		public override object Create(ActorInitializer init) { return new RepairsUnits(this); }
	}

	public class RepairsUnits : PausableConditionalTrait<RepairsUnitsInfo>
	{
		public RepairsUnits(RepairsUnitsInfo info)
			: base(info) { }
	}
}
