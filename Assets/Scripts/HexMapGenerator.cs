using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{
    [Range(20, 200)]
    public int chunkSizeMin = 30;

    [Range(20, 200)]
    public int chunkSizeMax = 100;

    public HexGrid grid;

    [Range(0f, 0.5f)]
    public float jitterProbability = 0.25f;

    [Range(5, 95)]
    public int landPercentage = 50;
    
    [Range(1, 5)]
    public int waterLevel = 3;


    int cellCount;
    PriortyQueue<HexCell> searchFrontier = new PriortyQueue<HexCell>();
    int searchFrontierPhase;

    void CreateLand()
    {
        int landBudget = Mathf.RoundToInt(cellCount * landPercentage * 0.01f);

        while (landBudget > 0)
        {
            landBudget = RaiseTerrain(
                Random.Range(chunkSizeMin, chunkSizeMax + 1), landBudget
            );
        }
    }

    public void GenerateMap(int x, int z)
    {
        cellCount = x * z;

        if (searchFrontier == null)
        {
            searchFrontier = new PriortyQueue<HexCell>();
        }

        grid.CreateMap(x, z);
        for (int i = 0; i < cellCount; i++)
        {
            grid.GetCell(i).WaterLevel = waterLevel;
        }
        CreateLand();
        SetTerrainType();

        for (int i = 0; i < cellCount; i++)
        {
            grid.GetCell(i).SearchSeed = 0;
        }
    }

    HexCell GetRandomCell()
    {
        return grid.GetCell(Random.Range(0, cellCount));
    }

    int RaiseTerrain(int chunkSize, int budget)
    {
        searchFrontierPhase += 1;
        HexCell firstCell = GetRandomCell();
        firstCell.SearchSeed = searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        searchFrontier.Enqueue(firstCell);
        HexCoordinates center = firstCell.Coordinates;

        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0)
        {
            HexCell current = searchFrontier.Dequeue();
            current.Elevation += 1;

            if (current.Elevation == waterLevel && --budget == 0)
            {
                break;
            }

            size += 1;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = current.GetNeighbor(d);
                if (neighbor && neighbor.SearchSeed < searchFrontierPhase)
                {
                    neighbor.SearchSeed = searchFrontierPhase;
                    neighbor.Distance = neighbor.Coordinates.DistanceTo(center);
                    neighbor.SearchHeuristic = Random.value < jitterProbability ? 1 : 0;
                    searchFrontier.Enqueue(neighbor);
                }
            }
        }
        searchFrontier.Clear();
        return budget;
    }

    void SetTerrainType()
    {
        for (int i = 0; i < cellCount; i++)
        {
            HexCell cell = grid.GetCell(i);
            if (!cell.IsUnderwater)
            {
                cell.TerrainTypeIndex = cell.Elevation - cell.WaterLevel;
            }
        }
    }
}