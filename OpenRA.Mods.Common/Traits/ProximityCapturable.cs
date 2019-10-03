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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public enum ProximityCapturableType { Range, Area }

	[Desc("Actor can be captured by units in a specified proximity.")]
	public class ProximityCapturableInfo : TraitInfo, IRulesetLoaded
	{
		[Desc("Whether the captor needs to be within a certain range or within a certain set of cells to capture the actor.")]
		public readonly ProximityCapturableType Type = ProximityCapturableType.Range;

		[Desc("Maximum range at which a ProximityCaptor actor can initiate the capture. Requires 'Type' set to 'Range'.")]
		public readonly WDist Range = WDist.FromCells(5);

		[Desc("Set of cells the ProximityCaptor needs to be in to initiate the capture. Requires 'Type' set to 'Area'. ",
			"If empty, the immediately neighboring cells of the actor will be used, unless an 'Area' ActorInit is defined.")]
		public readonly CPos[] Area = { };

		[Desc("Allowed ProximityCaptor actors to capture this actor.")]
		public readonly BitSet<CaptureType> CaptorTypes = new BitSet<CaptureType>("Player", "Vehicle", "Tank", "Infantry");

		[Desc("If set, the capturing process stops immediately after another player comes into Range.")]
		public readonly bool MustBeClear = false;

		[Desc("If set, the ownership will not revert back when the captor leaves the area.")]
		public readonly bool Sticky = false;

		[Desc("If set, the actor can only be captured via this logic once.",
			"This option implies the `Sticky` behaviour as well.")]
		public readonly bool Permanent = false;

		public void RulesetLoaded(Ruleset rules, ActorInfo info)
		{
			var pci = rules.Actors["player"].TraitInfoOrDefault<ProximityCaptorInfo>();
			if (pci == null)
				throw new YamlException("ProximityCapturable requires the `Player` actor to have the ProximityCaptor trait.");
		}

		public override object Create(ActorInitializer init) { return new ProximityCapturable(init, this); }
	}

	public class ProximityCapturable : ITick, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyOwnerChanged
	{
		public readonly Player OriginalOwner;
		public bool Captured { get { return Self.Owner != OriginalOwner; } }

		public ProximityCapturableInfo Info;
		public Actor Self;

		readonly List<Actor> actorsInRange = new List<Actor>();
		CPos[] area;
		int trigger;
		WPos prevPosition;
		bool skipTriggerUpdate;

		public ProximityCapturable(ActorInitializer init, ProximityCapturableInfo info)
		{
			Info = info;
			Self = init.Self;
			OriginalOwner = Self.Owner;
			area = init.GetValue<AreaInit, CPos[]>(info, info.Area);
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			if (skipTriggerUpdate)
				return;

			if (Info.Type == ProximityCapturableType.Range)
				trigger = self.World.ActorMap.AddProximityTrigger(self.CenterPosition, Info.Range, WDist.Zero, ActorEntered, ActorLeft);
			else
			{
				if (area.Length == 0)
					area = Util.ExpandFootprint(new List<CPos> { self.Location }, true).ToArray();

				trigger = self.World.ActorMap.AddCellTrigger(area, ActorEntered, ActorLeft);
			}
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (skipTriggerUpdate)
				return;

			if (Info.Type == ProximityCapturableType.Range)
				self.World.ActorMap.RemoveProximityTrigger(trigger);
			else
				self.World.ActorMap.RemoveCellTrigger(trigger);

			actorsInRange.Clear();
		}

		void ITick.Tick(Actor self)
		{
			if (!self.IsInWorld || self.CenterPosition == prevPosition)
				return;

			if (Info.Type == ProximityCapturableType.Range)
				self.World.ActorMap.UpdateProximityTrigger(trigger, self.CenterPosition, Info.Range, WDist.Zero);

			prevPosition = self.CenterPosition;
		}

		void ActorEntered(Actor other)
		{
			if (skipTriggerUpdate || !CanBeCapturedBy(other))
				return;

			actorsInRange.Add(other);
			UpdateOwnership();
		}

		void ActorLeft(Actor other)
		{
			if (skipTriggerUpdate || !CanBeCapturedBy(other))
				return;

			actorsInRange.Remove(other);
			UpdateOwnership();
		}

		bool CanBeCapturedBy(Actor a)
		{
			if (a == Self)
				return false;

			var pc = a.Info.TraitInfoOrDefault<ProximityCaptorInfo>();
			return pc != null && pc.Types.Overlaps(Info.CaptorTypes);
		}

		bool IsClear(Actor self, Player captorOwner)
		{
			return actorsInRange
				.All(a => a.Owner == captorOwner || WorldUtils.AreMutualAllies(a.Owner, captorOwner));
		}

		void UpdateOwnership()
		{
			if (Captured && Info.Permanent)
			{
				// This area has been captured and cannot ever be re-captured, so we get rid of the
				// trigger and ensure that it won't be recreated in AddedToWorld.
				skipTriggerUpdate = true;
				if (Info.Type == ProximityCapturableType.Range)
					Self.World.ActorMap.RemoveProximityTrigger(trigger);
				else
					Self.World.ActorMap.RemoveCellTrigger(trigger);

				return;
			}

			// The actor that has been in the area the longest will be the captor.
			// The previous implementation used the closest one, but that doesn't work with
			// ProximityTriggers since they only generate events when actors enter or leave.
			var captor = actorsInRange.FirstOrDefault();

			// The last unit left the area
			if (captor == null)
			{
				// Unless the Sticky option is set, we revert to the original owner.
				if (Captured && !Info.Sticky)
					ChangeOwnership(Self, OriginalOwner.PlayerActor);
			}
			else
			{
				if (Info.MustBeClear)
				{
					var isClear = IsClear(Self, captor.Owner);

					// An enemy unit has wandered into the area, so we've lost control of it.
					if (Captured && !isClear)
						ChangeOwnership(Self, OriginalOwner.PlayerActor);

					// We don't own the area yet, but it is clear from enemy units, so we take possession of it.
					else if (Self.Owner != captor.Owner && isClear)
						ChangeOwnership(Self, captor);
				}
				else
				{
					// In all other cases, we just take over.
					if (Self.Owner != captor.Owner)
						ChangeOwnership(Self, captor);
				}
			}
		}

		void ChangeOwnership(Actor self, Actor captor)
		{
			self.World.AddFrameEndTask(w =>
			{
				if (self.Disposed || captor.Disposed)
					return;

				// prevent (Added|Removed)FromWorld from firing during Actor.ChangeOwner
				skipTriggerUpdate = true;
				var previousOwner = self.Owner;
				self.ChangeOwner(captor.Owner);

				if (self.Owner == self.World.LocalPlayer)
					w.Add(new FlashTarget(self));

				var pc = captor.Info.TraitInfoOrDefault<ProximityCaptorInfo>();
				foreach (var t in self.TraitsImplementing<INotifyCapture>())
					t.OnCapture(self, captor, previousOwner, captor.Owner, pc.Types);
			});
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			Game.RunAfterTick(() => skipTriggerUpdate = false);
		}
	}

	public class AreaInit : ValueActorInit<CPos[]>
	{
		public AreaInit(CPos[] value)
			: base(value) { }
	}
}
