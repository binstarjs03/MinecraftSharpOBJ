﻿/*
Copyright (c) 2022, Bintang Jakasurya
All rights reserved. 

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections.Generic;

using binstarjs03.AerialOBJ.Core.Primitives;
using binstarjs03.AerialOBJ.Core.Nbt;

namespace binstarjs03.AerialOBJ.Core.MinecraftWorld;

public class Block
{
    public const string AirBlockName = "minecraft:air";
    public const string AirCaveBlockName = "minecraft:cave_air";
    private string _name;
    private Point3<int> _blockCoordsAbs;
    private Dictionary<string, string>? _properties;

    public static Block Air => new();

    public string Name
    {
        get => _name;
        set => _name = value;
    }

    public Point3<int> BlockCoordsAbs
    {
        get => _blockCoordsAbs;
        set => _blockCoordsAbs = value;
    }

    public Dictionary<string, string>? Properties
    {
        get => _properties;
        set => _properties = value;
    }

    public Block()
    {
        _name = AirBlockName;
        _blockCoordsAbs = Point3<int>.Zero;
    }

    // propertiesless and nameless constructor
    public Block(Point3<int> coordsAbs)
    {
        _name = AirBlockName;
        _blockCoordsAbs = coordsAbs;
    }

    // propertiesless constructor
    public Block(string name, Point3<int> coordsAbs)
    {
        _name = name;
        _blockCoordsAbs = coordsAbs;
    }

    // TODO properties parser isn't implemented yet. Any properties from nbt
    // compound will be ignored and not stored inside properties dictionary
    public Block(Point3<int> coordsAbs, NbtCompound properties)
    {
        _name = properties.Get<NbtString>("Name").Value;
        _blockCoordsAbs = coordsAbs;
    }

    public static bool IsAir(Block block)
    {
        return block.Name == AirBlockName || block.Name == AirCaveBlockName;
    }

    public static bool IsAir(string blockName)
    {
        return blockName == AirBlockName || blockName == AirCaveBlockName;
    }

    public override string ToString()
    {
        return $"Block {_name} at {_blockCoordsAbs}";
    }
}
