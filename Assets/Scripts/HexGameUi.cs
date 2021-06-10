using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class HexGameUi : MonoBehaviour
{
    public HexGrid grid;
    HexUnit previousUnit;
    HexCell currentCell;
    HexUnit selectedUnit;

    void DoMove()
    {
        if (grid.HasPath)
        {
            selectedUnit.Travel(grid.CurrentPath);
            grid.ClearPath();
        }
    }

    void DoPathfinding()
    {
        if (UpdateCurrentCell())
        {
            if (currentCell && selectedUnit.IsValidDestination(currentCell))
            {
                grid.FindPath(selectedUnit.Location, currentCell, selectedUnit);
            }
            else
            {
                grid.ClearPath();
            }
            selectedUnit.Location.EnableHighlight(Color.blue);
        }
    }

    void DoSelection()
    {
        grid.ClearPath();
        UpdateCurrentCell();
        if (currentCell)
        {
            previousUnit = selectedUnit;
            if(previousUnit != null)
            {
                previousUnit.Location.DisableHighlight();
            }
            selectedUnit = currentCell.Unit;
            selectedUnit.Location.EnableHighlight(Color.blue);
        }
    }

    public void SetEditMode(bool toggle)
    {
        grid.ClearPath();
        enabled = !toggle;
        grid.ShowUI(!toggle);
        if (toggle)
        {
            Shader.EnableKeyword("HEX_MAP_EDIT_MODE");
        }
        else
        {
            Shader.DisableKeyword("HEX_MAP_EDIT_MODE");
        }
    }

    void Update()
    {
        //I don't think my UI is set up correctly so I'm getting a bunch of
        //false positives WRT IsPointerOverGameObject.  Doing direct raycast comparison
        //for now.
        HexCell cell = grid.GetCell(Camera.main.ScreenPointToRay(Input.mousePosition));
        if (cell != null)
        {
            if (cell.Unit != null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    DoSelection();
                }
            }
            if (selectedUnit)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    DoMove();
                }
                else
                {
                    DoPathfinding();
                }
            }
        }
        //if (!EventSystem.current.IsPointerOverGameObject())
        //{
        //    if (Input.GetMouseButtonDown(0))
        //    {
        //        DoSelection();
        //    }
        //    else if (selectedUnit)
        //    {
        //        if (Input.GetMouseButtonDown(1))
        //        {
        //            DoMove();
        //        }
        //        else
        //        {
        //            DoPathfinding();
        //        }
        //    }
        //}
    }

    bool UpdateCurrentCell()
    {
        HexCell cell =
            grid.GetCell(Camera.main.ScreenPointToRay(Input.mousePosition));
        if (cell != currentCell)
        {
            currentCell = cell;
            return true;
        }
        return false;
    }
}
