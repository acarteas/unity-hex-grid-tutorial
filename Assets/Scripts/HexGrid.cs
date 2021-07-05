using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class HexGrid : MonoBehaviour
{
    public HexUnit unitPrefab;
    public int cellCountX = 20, cellCountZ = 15;
    public int seed;
    int chunkCountX, chunkCountZ;
    public bool wrapping = true;
    Transform[] columns = new Transform[0];
    int currentCenterColumnIndex = -1;

    public HexCell cellPrefab;
    public Text cellLabelPrefab;
    public HexGridChunk chunkPrefab;
    public Texture2D NoiseSource;

    HexGridChunk[] chunks;
    HexCell[] cells;
    public Color[] colors;
    List<HexUnit> units = new List<HexUnit>();
    Dictionary<HexCell, Dictionary<HexCell, List<HexCell>>> _paths = new Dictionary<HexCell, Dictionary<HexCell, List<HexCell>>>();
    List<HexCell> _previousPath = new List<HexCell>();
    HexCellShaderData cellShaderData;

    public List<HexCell> CurrentPath
    {
        get => _previousPath;
    }

    public bool HasPath
    {
        get
        {
            return _previousPath.Count > 0;
        }
    }

    void Awake()
    {
        HexMetrics.noiseSource = NoiseSource;
        HexMetrics.InitializeHashGrid(seed);
        HexUnit.unitPrefab = unitPrefab;
        cellShaderData = gameObject.AddComponent<HexCellShaderData>();
        cellShaderData.Grid = this;
        CreateMap(cellCountX, cellCountZ, this.wrapping);
    }
    public void ResetVisibility()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].ResetVisibility();
        }
        for (int i = 0; i < units.Count; i++)
        {
            HexUnit unit = units[i];
            IncreaseVisibility(unit.Location, unit.VisionRange);
        }
    }

    public HexCell GetCell(int xOffset, int zOffset)
    {
        return cells[xOffset + zOffset * cellCountX];
    }

    public HexCell GetCell(int cellIndex)
    {
        return cells[cellIndex];
    }

    public void CenterMap(float xPosition)
    {
        int centerColumnIndex = (int)
            (xPosition / (HexMetrics.innerDiameter * HexMetrics.chunkSizeX));

        if (centerColumnIndex == currentCenterColumnIndex)
        {
            return;
        }
        currentCenterColumnIndex = centerColumnIndex;
        int minColumnIndex = centerColumnIndex - chunkCountX / 2;
        int maxColumnIndex = centerColumnIndex + chunkCountX / 2;
        Vector3 position;
        position.y = position.z = 0f;
        for (int i = 0; i < columns.Length; i++)
        {
            if (i < minColumnIndex)
            {
                position.x = chunkCountX *
                    (HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
            }
            else if (i > maxColumnIndex)
            {
                position.x = chunkCountX *
                    -(HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
            }
            else
            {
                position.x = 0f;
            }
            columns[i].localPosition = position;
        }
    }

    void ClearUnits()
    {
        for (int i = 0; i < units.Count; i++)
        {
            units[i].Die();
        }
        units.Clear();
    }

    public bool CreateMap(int x, int z, bool wrapping)
    {
        if (
            x <= 0 || x % HexMetrics.chunkSizeX != 0 ||
            z <= 0 || z % HexMetrics.chunkSizeZ != 0
        )
        {
            Debug.LogError("Unsupported map size.");
            return false;
        }
        ClearUnits();
        cellCountX = x;
        cellCountZ = z;
        this.wrapping = wrapping;
        currentCenterColumnIndex = -1;
        HexMetrics.wrapSize = wrapping ? cellCountX : 0;
        if (columns != null)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                Destroy(columns[i].gameObject);
            }
        }

        chunkCountX = cellCountX / HexMetrics.chunkSizeX;
        chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;
        cellShaderData.Initialize(cellCountX, cellCountZ);
        CreateChunks();
        CreateCells();
        return true;
    }
    public void Save(BinaryWriter writer)
    {
        writer.Write(cellCountX);
        writer.Write(cellCountZ);
        writer.Write(wrapping);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].Save(writer);
        }

        writer.Write(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            units[i].Save(writer);
        }
    }

    public void Load(BinaryReader reader, int header)
    {
        StopAllCoroutines();
        ClearUnits();
        int x = 20, z = 15;
        if (header >= 1)
        {
            x = reader.ReadInt32();
            z = reader.ReadInt32();
        }
        bool wrapping = header >= 5 ? reader.ReadBoolean() : false;
        if (x != cellCountX || z != cellCountZ || this.wrapping != wrapping)
        {
            if (!CreateMap(x, z, wrapping))
            {
                return;
            }
        }

        bool originalImmediateMode = cellShaderData.ImmediateMode;
        cellShaderData.ImmediateMode = true;
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].Load(reader, header);
        }
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i].Refresh();
        }
        if (header >= 2)
        {
            int unitCount = reader.ReadInt32();
            for (int i = 0; i < unitCount; i++)
            {
                HexUnit.Load(reader, this);
            }
        }
        cellShaderData.ImmediateMode = originalImmediateMode;
    }

    public HexCell GetCell(Ray ray)
    {
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            return GetCell(hit.point);
        }
        return null;
    }

    public void MakeChildOfColumn(Transform child, int columnIndex)
    {
        child.SetParent(columns[columnIndex], false);
    }

    public void AddUnit(HexUnit unit, HexCell location, float orientation)
    {
        units.Add(unit);
        unit.Grid = this;

        unit.Location = location;
        unit.Orientation = orientation;
    }

    public void RemoveUnit(HexUnit unit)
    {
        units.Remove(unit);
        unit.Die();
    }


    void CreateChunks()
    {
        columns = new Transform[chunkCountX];
        for (int x = 0; x < chunkCountX; x++)
        {
            columns[x] = new GameObject("Column").transform;
            columns[x].SetParent(transform, false);
        }

        chunks = new HexGridChunk[chunkCountX * chunkCountZ];

        for (int z = 0, i = 0; z < chunkCountZ; z++)
        {
            for (int x = 0; x < chunkCountX; x++)
            {
                HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
                chunk.transform.SetParent(columns[x], false);
            }
        }
    }

    void CreateCells()
    {
        cells = new HexCell[cellCountZ * cellCountX];

        for (int z = 0, i = 0; z < cellCountZ; z++)
        {
            for (int x = 0; x < cellCountX; x++)
            {
                CreateCell(x, z, i++);
            }
        }
    }

    void CreateCell(int x, int z, int i)
    {
        Vector3 position;
        position.x = (x + z * 0.5f - z / 2) * HexMetrics.innerDiameter;
        position.y = 0f;
        position.z = z * (HexMetrics.outerRadius * 1.5f);

        HexCell cell = cells[i] = Instantiate<HexCell>(cellPrefab);
        cell.transform.localPosition = position;
        cell.Coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
        cell.Index = i;
        cell.ColumnIndex = x / HexMetrics.chunkSizeX;
        cell.ShaderData = cellShaderData;
        if (wrapping)
        {
            cell.CanBeExplored = z > 0 && z < cellCountZ - 1;
        }
        else
        {
            cell.CanBeExplored =
                x > 0 && z > 0 && x < cellCountX - 1 && z < cellCountZ - 1;
        }

        if (x > 0)
        {
            cell.SetNeighbor(HexDirection.W, cells[i - 1]);
            if (wrapping && x == cellCountX - 1)
            {
                cell.SetNeighbor(HexDirection.E, cells[i - x]);
            }
        }
        if (z > 0)
        {
            if ((z & 1) == 0)
            {
                cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX]);
                if (x > 0)
                {
                    cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX - 1]);
                }
                else if (wrapping)
                {
                    cell.SetNeighbor(HexDirection.SW, cells[i - 1]);
                }
            }
            else
            {
                cell.SetNeighbor(HexDirection.SW, cells[i - cellCountX]);
                if (x < cellCountX - 1)
                {
                    cell.SetNeighbor(HexDirection.SE, cells[i - cellCountX + 1]);
                }
                else if (wrapping)
                {
                    cell.SetNeighbor(
                        HexDirection.SE, cells[i - cellCountX * 2 + 1]
                    );
                }
            }
        }

        Text label = Instantiate<Text>(cellLabelPrefab);
        label.rectTransform.anchoredPosition =
            new Vector2(position.x, position.z);
        cell.UiRect = label.rectTransform;

        cell.Elevation = 0;

        AddCellToChunk(x, z, cell);
    }

    public void FindPath(HexCell origin, HexCell destination, HexUnit unit)
    {
        ClearPath(_previousPath);
        ListPool<HexCell>.Add(_previousPath);
        if (_paths.ContainsKey(origin) && _paths[origin].ContainsKey(destination))
        {
            //HighlightPath(_paths[origin][destination], unit);
        }
        else
        {
            _previousPath = Search(origin, destination, unit);

            //turned off for now
            //RememberPath(origin, destination, _previousPath);
        }
        HighlightPath(_previousPath, unit.Speed);
    }

    public void ClearPath()
    {
        ClearPath(_previousPath);
        _previousPath = new List<HexCell>();
    }

    void ClearPath(List<HexCell> path)
    {
        for (int i = 0; i < path.Count; i++)
        {
            path[i].Distance = int.MaxValue;
            path[i].SetLabel(null);
            path[i].DisableHighlight();
        }
    }


    int _search_counter = int.MinValue;
    public int SearchCounter
    {
        get
        {
            _search_counter++;
            if (_search_counter >= int.MaxValue - 1)
            {
                _search_counter = int.MinValue;
            }
            return _search_counter;
        }
    }

    List<HexCell> Search(HexCell origin, HexCell destination, HexUnit unit)
    {
        bool found = false;

        HashSet<HexCell> seen = new HashSet<HexCell>();
        PriortyQueue<HexCell> queue = new PriortyQueue<HexCell>();
        queue.Enqueue(origin);
        origin.Distance = 0;
        origin.SearchSeed = SearchCounter;
        while (queue.Count > 0 && found == false)
        {
            var current = queue.Dequeue();

            //skip previously seen cells
            if (seen.Contains(current))
            {
                continue;
            }
            seen.Add(current);

            int currentTurn = (current.Distance - 1) / unit.Speed;
            for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
            {
                HexCell neighbor = current.GetNeighbor(direction);

                //skip visited nodes
                if (neighbor == null || neighbor.SearchSeed == current.SearchSeed)
                {
                    continue;
                }

                if (!unit.IsValidDestination(neighbor))
                {
                    continue;
                }
                int movementCost = unit.GetMoveCost(current, neighbor, direction);
                if (movementCost < 0)
                {
                    continue;
                }

                int distance = current.Distance + movementCost;
                int turn = (distance - 1) / unit.Speed;
                if (turn > currentTurn)
                {
                    distance = turn * unit.Speed + movementCost;
                }

                neighbor.SearchSeed = current.SearchSeed;
                neighbor.Distance = distance;
                neighbor.PathFrom = current;
                neighbor.SearchHeuristic =
                    neighbor.Coordinates.DistanceTo(destination.Coordinates);
                queue.Enqueue(neighbor);

                if (neighbor == destination)
                {
                    found = true;
                    break;
                }
            }
        }

        //build path if one was found
        if (found)
        {
            //build a path where path[0] = dest and path[n] = origin
            List<HexCell> path = ListPool<HexCell>.Get();
            HexCell current = destination;
            while (current != origin)
            {
                path.Add(current);
                current = current.PathFrom;
            }
            path.Add(current);
            path.Reverse();
            return path;
        }
        return ListPool<HexCell>.Get();
    }

    public void IncreaseVisibility(HexCell fromCell, int range)
    {
        List<HexCell> cells = GetVisibleCells(fromCell, range);
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].IncreaseVisibility();
        }
        ListPool<HexCell>.Add(cells);
    }

    public void DecreaseVisibility(HexCell fromCell, int range)
    {
        List<HexCell> cells = GetVisibleCells(fromCell, range);
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].DecreaseVisibility();
        }
        ListPool<HexCell>.Add(cells);
    }

    List<HexCell> GetVisibleCells(HexCell origin, int range)
    {
        range += origin.ViewElevation;
        HashSet<HexCell> seen = new HashSet<HexCell>();
        PriortyQueue<HexCell> queue = new PriortyQueue<HexCell>();
        queue.Enqueue(origin);
        origin.Distance = 0;
        origin.SearchSeed = SearchCounter;
        origin.SearchHeuristic = 0;
        HexCoordinates originCoordinates = origin.Coordinates;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            //skip previously seen cells
            if (seen.Contains(current))
            {
                continue;
            }
            seen.Add(current);
            for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
            {
                HexCell neighbor = current.GetNeighbor(direction);

                //skip visited nodes or not explorable
                if (neighbor == null || neighbor.SearchSeed == current.SearchSeed 
                    || neighbor.CanBeExplored == false)
                {
                    continue;
                }

                int distance = current.Distance + 1;
                if (distance + neighbor.ViewElevation > range
                    ||
                    distance > originCoordinates.DistanceTo(neighbor.Coordinates))
                {
                    continue;
                }
                if (distance < range)
                {
                    neighbor.SearchHeuristic = 0;
                    neighbor.SearchSeed = current.SearchSeed;
                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return seen.ToList();
    }

    void RememberPath(HexCell origin, HexCell destination, List<HexCell> path)
    {
        if (_paths.ContainsKey(origin) == false)
        {
            _paths.Add(origin, new Dictionary<HexCell, List<HexCell>>());
        }
        if (_paths[origin].ContainsKey(destination) == false)
        {
            _paths[origin][destination] = new List<HexCell>();
        }
        _paths[origin][destination] = path;
    }

    void HighlightPath(List<HexCell> path, int movementAllowance)
    {
        if (path.Count < 2)
        {
            return;
        }

        //handle destination
        HexCell current = path[path.Count - 1];
        int turn = (current.Distance - 1) / movementAllowance;
        current.SetLabel(turn.ToString());
        current.EnableHighlight(Color.red);

        //handle origin
        current = path[0];
        turn = 0;
        current.SetLabel(turn.ToString());
        current.EnableHighlight(Color.blue);

        //handle intermediate steps
        for (int i = path.Count - 2; i > 0; i--)
        {
            current = path[i];
            turn = (current.Distance - 1) / movementAllowance;
            current.SetLabel(turn.ToString());
            current.EnableHighlight(Color.white);
        }
    }

    void AddCellToChunk(int x, int z, HexCell cell)
    {
        int chunkX = x / HexMetrics.chunkSizeX;
        int chunkZ = z / HexMetrics.chunkSizeZ;
        HexGridChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

        int localX = x - chunkX * HexMetrics.chunkSizeX;
        int localZ = z - chunkZ * HexMetrics.chunkSizeZ;
        chunk.AddCell(localX + localZ * HexMetrics.chunkSizeX, cell);
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            HandleInput();
        }
    }

    void OnEnable()
    {
        if (!HexMetrics.noiseSource)
        {
            HexMetrics.noiseSource = NoiseSource;
            HexMetrics.InitializeHashGrid(seed);
            HexUnit.unitPrefab = unitPrefab;
            HexMetrics.wrapSize = wrapping ? cellCountX : 0;
            ResetVisibility();
        }
    }

    void HandleInput()
    {
        Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(inputRay, out hit))
        {
            //ColorCell(hit.point, touchedColor);
        }
    }


    public HexCell GetCell(Vector3 position)
    {
        position = transform.InverseTransformPoint(position);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);
        return GetCell(coordinates);
    }

    public void ColorCell(Vector3 position, Color color)
    {
        position = transform.InverseTransformPoint(position);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);
        int index = coordinates.X + coordinates.Z * cellCountX + coordinates.Z / 2;
        HexCell cell = cells[index];
        //hexMesh.Triangulate(cells);
    }

    public HexCell GetCell(HexCoordinates coordinates)
    {
        int z = coordinates.Z;
        if (z < 0 || z >= cellCountZ)
        {
            return null;
        }
        int x = coordinates.X + z / 2;
        if (x < 0 || x >= cellCountX)
        {
            return null;
        }
        return cells[x + z * cellCountX];
    }

    public void ShowUI(bool visible)
    {
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i].ShowUI(visible);
        }
    }
}
