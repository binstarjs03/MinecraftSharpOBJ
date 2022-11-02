﻿using System;
using System.IO;
using System.Collections.Generic;

using binstarjs03.AerialOBJ.Core.CoordinateSystem;
using binstarjs03.AerialOBJ.Core.Nbt;
using binstarjs03.AerialOBJ.Core.Nbt.IO;
using System.Collections.ObjectModel;

namespace binstarjs03.AerialOBJ.Core.WorldRegion;

public class Region
{
    public const int BlockCount = Section.BlockCount * ChunkCount;
    public const int ChunkCount = 32;
    public const int TotalChunkCount = ChunkCount * ChunkCount;
    public const int ChunkRange = ChunkCount - 1;
    public static readonly CoordsRange2 ChunkRangeRel = new(
        min: Coords2.Zero,
        max: new Coords2(ChunkRange, ChunkRange)
    );

    public const int SectorDataSize = 4096;
    public const int ChunkHeaderTableSize = SectorDataSize * 1;
    public const int ChunkHeaderSize = 4;

    private readonly string _path;
    private readonly byte[]? _data;
    private readonly Coords2 _coords;
    private readonly CoordsRange2 _chunkRangeAbs;
    
    public Coords2 Coords => _coords;
    public CoordsRange2 ChunkRangeAbs => _chunkRangeAbs;

    public Region(string path, Coords2 coords)
    {
        FileInfo fi = new(path);
        checkRegionData(fi);
        _path = path;
        _data = File.ReadAllBytes(path);
        _coords = coords;
        _chunkRangeAbs = calculateChunkRangeAbs(coords);

        static void checkRegionData(FileInfo fileInfo)
        {
            if (fileInfo.Length > ChunkHeaderTableSize)
                return;
            string msg = "Region data is too small";
            throw new InvalidDataException(msg);
        }

        static CoordsRange2 calculateChunkRangeAbs(Coords2 coords)
        {
            int minAbsCx = coords.X * ChunkCount;
            int minAbsCz = coords.Z * ChunkCount;
            Coords2 minAbsC = new(minAbsCx, minAbsCz);

            int maxAbsCx = minAbsCx + ChunkRange;
            int maxAbsCz = minAbsCz + ChunkRange;
            Coords2 maxAbsC = new(maxAbsCx, maxAbsCz);

            return new CoordsRange2(minAbsC, maxAbsC);
        }
    }

    public static Region Open(string path, Coords2 coords)
        => new(path, coords);

    /// <exception cref="RegionUnrecognizedFileException"></exception>
    public static Region Open(string path)
    {
        FileInfo fi = new(path);
        string[] split = fi.Name.Split('.');
        bool correctPrefix = split[0] == "r";
        bool correctFileType = split[3] == "mca";
        bool validCoordinate = int.TryParse(split[1], out _) && int.TryParse(split[2], out _);
        if (correctPrefix && correctFileType && validCoordinate)
        {
            int x = int.Parse(split[1]);
            int z = int.Parse(split[2]);
            Coords2 coords = new(x, z);
            return new Region(path, coords);
        }
        else
            throw new RegionUnrecognizedFileException("Cannot automatically determine region position");
    }
    
    private ArraySegment<byte> Read(int pos, int count)
    {
        long endPos = pos + count;
        if (endPos > _data!.Length)
            throw new IndexOutOfRangeException("data is outside bounds");
        return new ArraySegment<byte>(_data!, pos, count);
    }

    public static Coords2 ConvertChunkCoordsAbsToRel(Coords2 coords)
    {
        int relCx = MathUtils.Mod(coords.X, ChunkCount);
        int relCz = MathUtils.Mod(coords.Z, ChunkCount);
        return new Coords2(relCx, relCz);
    }

    public static Coords2 GetRegionCoordsFromChunkCoordsAbs(Coords2 chunkCoordsAbs)
    {
        return new((int)MathF.Floor((float)chunkCoordsAbs.X / ChunkCount),
                   (int)MathF.Floor((float)chunkCoordsAbs.Z / ChunkCount));
    }

    public bool HasChunkGenerated(Coords2 chunkCoordsRel)
    {
        GetChunkHeaderData(chunkCoordsRel, out int sectorPos, out int sectorLength);
        return HasChunkGenerated(sectorPos, sectorLength);
    }

    private static bool HasChunkGenerated(int sectorPos, int sectorLength)
    {
        if (sectorPos == 0 && sectorLength == 0)
            return false;
        return true;
    }

    private void GetChunkHeaderData(Coords2 chunkCoordsRel, out int sectorPos, out int sectorLength)
    {
        ChunkRangeRel.ThrowIfOutside(chunkCoordsRel);

        int seekPos = (chunkCoordsRel.X + chunkCoordsRel.Z * ChunkCount) * ChunkHeaderSize;

        // original code
        //byte[] chunkHeader = Read(seekPos, ChunkHeaderSize);
        //int chunkPos = BinaryPrimitives.ReadInt32BigEndian(new byte[1].Concat(chunkHeader[0..3]).ToArray());
        //int chunkLength = chunkHeader[3];

        // more unreadable version, it does not allocate heap, prevent GC
        ArraySegment<byte> chunkHeaderSegment = Read(seekPos, 4);
        sectorPos = 0;
        for (int i = 0; i < 3; i++)
        {
            int buff = chunkHeaderSegment[i];
            buff = buff << (3 - i - 1) * 8;
            sectorPos += buff;
        }
        sectorLength = chunkHeaderSegment[3];
    }

    public ReadOnlyCollection<Coords2> GetGeneratedChunksAsCoordsRel()
    {
        List<Coords2> generatedChunks = new(TotalChunkCount);
        for (int x = 0; x < ChunkCount; x++)
        {
            for (int z = 0; z < ChunkCount; z++)
            {
                Coords2 coordsChunk = new(x, z);
                if (HasChunkGenerated(coordsChunk))
                    generatedChunks.Add(coordsChunk);
            }
        }
        generatedChunks.TrimExcess();
        return generatedChunks.AsReadOnly();
    }

    public HashSet<Coords2> GetGeneratedChunksAsCoordsRelSet()
    {
        HashSet<Coords2> generatedChunks = new(TotalChunkCount);
        for (int x = 0; x < ChunkCount; x++)
        {
            for (int z = 0; z < ChunkCount; z++)
            {
                Coords2 coordsChunk = new(x, z);
                if (HasChunkGenerated(coordsChunk))
                    generatedChunks.Add(coordsChunk);
            }
        }
        generatedChunks.TrimExcess();
        return generatedChunks;
    }

    public NbtCompound GetChunkNbt(Coords2 chunkCoords, bool relative)
    {
        Coords2 chunkCoordsRel;
        if (relative)
        {
            ChunkRangeRel.ThrowIfOutside(chunkCoords);
            chunkCoordsRel = chunkCoords;
        }
        else
        {
            ChunkRangeAbs.ThrowIfOutside(chunkCoords);
            chunkCoordsRel = ConvertChunkCoordsAbsToRel(chunkCoords);
        }

        GetChunkHeaderData(chunkCoordsRel, out int sectorPos, out int sectorLength);
        if (!HasChunkGenerated(sectorPos, sectorLength))
        {
            string msg = $"Chunk is not generated yet";
            throw new ChunkNotGeneratedException(msg);
        }

        int seekPos = sectorPos * SectorDataSize;
        int dataLength = sectorLength * SectorDataSize;

        using MemoryStream chunkSectorStream = new(_data!, seekPos, dataLength, false);
        using IO.BinaryReaderEndian reader = new(chunkSectorStream);
        int chunkNbtLength = reader.ReadIntBE();
        chunkNbtLength -= 1;
        NbtCompression.Method compressionMethod = (NbtCompression.Method)reader.ReadByte();
        int chunkNbtDataPos = (int)(seekPos + chunkSectorStream.Position);
        int chunkNbtDataLength = (int)(dataLength - chunkSectorStream.Position);

        NbtCompound chunkNbt = (NbtCompound)NbtBase.ReadStream(
            new MemoryStream(_data!, chunkNbtDataPos, chunkNbtDataLength, false),
            compressionMethod);
        return chunkNbt;
    }

    public Chunk GetChunk(Coords2 chunkCoords, bool relative)
    {
        return new Chunk(GetChunkNbt(chunkCoords, relative));
    }

    public override string ToString()
    {
        return $"Region {Coords} at \"{_path}\"";
    }
}
