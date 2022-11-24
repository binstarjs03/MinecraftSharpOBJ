﻿using binstarjs03.AerialOBJ.Core.Primitives;
using binstarjs03.AerialOBJ.Core.Visualization.TwoDimension;

namespace binstarjs03.AerialOBJ.WpfAppNew.Components;

public class ChunkRegionViewport : ChunkViewport2<RegionImage>
{
    public ChunkRegionViewport(Size<int> screenSize) : base(screenSize)
    {
    }
}