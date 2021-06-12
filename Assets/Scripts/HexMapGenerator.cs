using System.Collections.Generic;
using UnityEngine;

public class HexMapGenerator : MonoBehaviour
{

	public HexGrid grid;

	public void GenerateMap(int x, int z)
	{
		grid.CreateMap(x, z);
	}
}