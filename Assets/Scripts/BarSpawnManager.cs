using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarSpawnManager : MonoBehaviour
{
    public GameObject MovingBarPrefab;
    public int BarCount = 5;

    void Start()
    {
        StartCoroutine(SpawnBar());
    }

    IEnumerator SpawnBar()
    {
        for (int i = 0; i < BarCount; i++)
        {
            Instantiate(MovingBarPrefab, transform);
            yield return new WaitForSeconds(2);
        }
    }
 
}
