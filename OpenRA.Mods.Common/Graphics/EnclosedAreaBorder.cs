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
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public struct EnclosedAreaBorder : IRenderable, IFinalizedRenderable
	{
		// Maps a cell offset to the index of the corner (in the 'Corner' arrays in the MapGrid.CellRamp structs)
		// from which a border should be drawn. The index of the end corner will be (cornerIndex + 1) % 4.
		static Dictionary<CVec, int> offset2CornerIndex = new Dictionary<CVec, int>
		{
			{ new CVec(0, -1), 0 },
			{ new CVec(1,  0), 1 },
			{ new CVec(0,  1), 2 },
			{ new CVec(-1, 0), 3 },
		};

		readonly CPos[] area;
		readonly Color color, contrastColor;

		public EnclosedAreaBorder(CPos[] area, Color color, Color contrastColor)
		{
			this.area = area;
			this.color = color;
			this.contrastColor = contrastColor;
		}

		WPos IRenderable.Pos { get { return WPos.Zero; } }
		PaletteReference IRenderable.Palette { get { return null; } }
		int IRenderable.ZOffset { get { return 0; } }
		bool IRenderable.IsDecoration { get { return true; } }

		IRenderable IRenderable.WithPalette(PaletteReference newPalette) { return new EnclosedAreaBorder(area, color, contrastColor); }
		IRenderable IRenderable.WithZOffset(int newOffset) { return new EnclosedAreaBorder(area, color, contrastColor); }
		IRenderable IRenderable.OffsetBy(WVec offset) { return new EnclosedAreaBorder(area, color, contrastColor); }
		IRenderable IRenderable.AsDecoration() { return this; }

		IFinalizedRenderable IRenderable.PrepareRender(WorldRenderer wr) { return this; }
		void IFinalizedRenderable.Render(WorldRenderer wr) { DrawAreaBorder(wr, area, 1, color, 3, contrastColor); }
		void IFinalizedRenderable.RenderDebugGeometry(WorldRenderer wr) { }
		Rectangle IFinalizedRenderable.ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }

		public static void DrawAreaBorder(WorldRenderer wr, CPos[] area, float width, Color color, float constrastWidth, Color constrastColor)
		{
			var map = wr.World.Map;
			var cr = Game.Renderer.RgbaColorRenderer;

			foreach (var c in area)
			{
				var mpos = c.ToMPos(map);
				if (!map.Height.Contains(mpos) || wr.World.ShroudObscures(c))
					continue;

				var tile = map.Tiles[mpos];
				var ti = map.Rules.TileSet.GetTileInfo(tile);
				var ramp = ti != null ? ti.RampType : 0;

				var corners = map.Grid.Ramps[ramp].Corners;
				var pos = map.CenterOfCell(c) - new WVec(0, 0, map.Grid.Ramps[ramp].CenterHeightOffset);

				foreach (var o in offset2CornerIndex)
				{
					// If the neighboring cell is part of the area, don't draw a border between the cells.
					if (area.Contains(c + o.Key))
						continue;

					var start = wr.Viewport.WorldToViewPx(wr.Screen3DPosition(pos + corners[o.Value]));
					var end = wr.Viewport.WorldToViewPx(wr.Screen3DPosition(pos + corners[(o.Value + 1) % 4]));

					if (constrastWidth > 0)
						cr.DrawLine(start, end, constrastWidth, constrastColor);

					if (width > 0)
						cr.DrawLine(start, end, width, color);
				}
			}
		}
	}
}
