using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ListPool<T>
{
    static Stack<List<T>> stack = new Stack<List<T>>();

	public static List<T> Get()
	{
		if (stack.Count > 0)
		{
			return stack.Pop();
		}
		return new List<T>();
	}

	/// <summary>
	/// Adds the supplied list back into the availability pool
	/// </summary>
	/// <param name="list"></param>
	public static void Add(List<T> list)
	{
		list.Clear();
		stack.Push(list);
	}
}
