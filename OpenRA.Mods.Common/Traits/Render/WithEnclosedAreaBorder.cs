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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	[Desc("Draw a border around a given set of cells.")]
	public class WithEnclosedAreaBorderInfo : TraitInfo
	{
		[Desc("List of cells comprising the area. The cells do not actually have to be contiguous.",
			"If an 'Area' ActorInit is given, it will be used instead.")]
		public readonly CPos[] Area = { };

		[Desc("Color for the border.")]
		public readonly Color Color = Color.White;

		[Desc("Contrast color for the border.")]
		public readonly Color ContrastColor = Color.Black;

		public override object Create(ActorInitializer init) { return new WithEnclosedAreaBorder(init, this); }
	}

	public class WithEnclosedAreaBorder : IRenderAnnotations
	{
		readonly WithEnclosedAreaBorderInfo info;
		readonly Actor self;

		readonly CPos[] area;

		public WithEnclosedAreaBorder(ActorInitializer init, WithEnclosedAreaBorderInfo info)
		{
			self = init.Self;
			this.info = info;

			area = init.GetValue<AreaInit, CPos[]>(info, info.Area);
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			yield return new EnclosedAreaBorder(area, info.Color, info.ContrastColor);
		}

		bool IRenderAnnotations.SpatiallyPartitionable { get { return false; } }
	}
}
