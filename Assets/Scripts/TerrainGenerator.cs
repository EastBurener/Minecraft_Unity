﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    public GameObject terrainChunk;

    public Transform player;

    public static Dictionary<ChunkPos, TerrainChunk> chunks = new Dictionary<ChunkPos, TerrainChunk>();

    FastNoise noise = new FastNoise();

    int chunkDist = 5;

    List<TerrainChunk> pooledChunks = new List<TerrainChunk>();

    List<ChunkPos> toGenerate = new List<ChunkPos>();

    // Start is called before the first frame update
    void Start()
    {
        LoadChunks(true);
    }


    private void Update()
    {
        LoadChunks();
    }



    void BuildChunk(int xPos, int zPos)
    {
        TerrainChunk chunk;
        if(pooledChunks.Count > 0)//对象池使用
        {
            chunk = pooledChunks[0];
            chunk.gameObject.SetActive(true);
            pooledChunks.RemoveAt(0);
            chunk.transform.position = new Vector3(xPos, 0, zPos);
        }
        else
        {
            GameObject chunkGO = Instantiate(terrainChunk, new Vector3(xPos, 0, zPos), Quaternion.identity);
            chunk = chunkGO.GetComponent<TerrainChunk>();
        }
        

        for(int x = 0; x < TerrainChunk.chunkWidth+2; x++)
            for(int z = 0; z < TerrainChunk.chunkWidth+2; z++)
                for(int y = 0; y < TerrainChunk.chunkHeight; y++)
                {
                    chunk.blocks[x, y, z] = GetBlockType(xPos+x-1, y, zPos+z-1);
                }


        GenerateTrees(chunk.blocks, xPos, zPos);

        chunk.BuildMesh();


        WaterChunk wat = chunk.transform.GetComponentInChildren<WaterChunk>();
        wat.SetLocs(chunk.blocks);
        wat.BuildMesh();
        


        chunks.Add(new ChunkPos(xPos, zPos), chunk);
    }

    
    BlockType GetBlockType(int x, int y, int z)
    {
        float simplex1 = noise.GetSimplex(x*.8f, z*.8f)*10;
        float simplex2 = noise.GetSimplex(x * 3f, z * 3f) * 10*(noise.GetSimplex(x*.3f, z*.3f)+.5f);

        float heightMap = simplex1 + simplex2;

        //地形区块使用二维噪声
        float baseLandHeight = TerrainChunk.chunkHeight * .5f + heightMap;

        //洞穴使用三维噪声
        float caveNoise1 = noise.GetPerlinFractal(x*5f, y*10f, z*5f);
        float caveMask = noise.GetSimplex(x * .3f, z * .3f)+.3f;

        //石头层级噪声
        float simplexStone1 = noise.GetSimplex(x * 1f, z * 1f) * 10;
        float simplexStone2 = (noise.GetSimplex(x * 5f, z * 5f)+.5f) * 20 * (noise.GetSimplex(x * .3f, z * .3f) + .5f);

        float stoneHeightMap = simplexStone1 + simplexStone2;
        float baseStoneHeight = TerrainChunk.chunkHeight * .25f + stoneHeightMap;
        
        BlockType blockType = BlockType.Air;
        
        if(y <= baseLandHeight)
        {
            blockType = BlockType.Dirt;

            //泥土顶部使用草方块
            if(y > baseLandHeight - 1 && y > WaterChunk.waterHeight-2)
                blockType = BlockType.Grass;

            if(y <= baseStoneHeight)
                blockType = BlockType.Stone;
        }


        if(caveNoise1 > Mathf.Max(caveMask, .2f))
            blockType = BlockType.Air;

        return blockType;
    }


    ChunkPos curChunk = new ChunkPos(-1,-1);
    void LoadChunks(bool instant = false)
    {
        int curChunkPosX = Mathf.FloorToInt(player.position.x/16)*16;
        int curChunkPosZ = Mathf.FloorToInt(player.position.z/16)*16;
        
        if(curChunk.x != curChunkPosX || curChunk.z != curChunkPosZ)
        {
            curChunk.x = curChunkPosX;
            curChunk.z = curChunkPosZ;


            for(int i = curChunkPosX - 16 * chunkDist; i <= curChunkPosX + 16 * chunkDist; i += 16)
                for(int j = curChunkPosZ - 16 * chunkDist; j <= curChunkPosZ + 16 * chunkDist; j += 16)
                {
                    ChunkPos cp = new ChunkPos(i, j);

                    if(!chunks.ContainsKey(cp) && !toGenerate.Contains(cp))
                    {
                        if(instant)
                            BuildChunk(i, j);
                        else
                            toGenerate.Add(cp);
                    }
                     

                }
            
            List<ChunkPos> toDestroy = new List<ChunkPos>();

            foreach(KeyValuePair<ChunkPos, TerrainChunk> c in chunks)
            {
                ChunkPos cp = c.Key;
                if(Mathf.Abs(curChunkPosX - cp.x) > 16 * (chunkDist + 3) || 
                    Mathf.Abs(curChunkPosZ - cp.z) > 16 * (chunkDist + 3))
                {
                    toDestroy.Add(c.Key);
                }
            }
            
            foreach(ChunkPos cp in toGenerate)
            {
                if(Mathf.Abs(curChunkPosX - cp.x) > 16 * (chunkDist + 1) ||
                    Mathf.Abs(curChunkPosZ - cp.z) > 16 * (chunkDist + 1))
                    toGenerate.Remove(cp);
            }

            foreach(ChunkPos cp in toDestroy)
            {
                chunks[cp].gameObject.SetActive(false);
                pooledChunks.Add(chunks[cp]);
                chunks.Remove(cp);
            }

            StartCoroutine(DelayBuildChunks());
        }




    }


    void GenerateTrees(BlockType[,,] blocks, int x, int z)
    {
        System.Random rand = new System.Random(x * 10000 + z);

        float simplex = noise.GetSimplex(x * .8f, z * .8f);

        if(simplex > 0)
        {
            simplex *= 2f;
            int treeCount = Mathf.FloorToInt((float)rand.NextDouble() * 5 * simplex);

            for(int i = 0; i < treeCount; i++)
            {
                int xPos = (int)(rand.NextDouble() * 14) + 1;
                int zPos = (int)(rand.NextDouble() * 14) + 1;

                int y = TerrainChunk.chunkHeight - 1;
                while(y > 0 && blocks[xPos, y, zPos] == BlockType.Air)
                {
                    y--;
                }
                y++;

                int treeHeight = 4 + (int)(rand.NextDouble() * 4);

                for(int j = 0; j < treeHeight; j++)
                {
                    if(y+j < 64)
                        blocks[xPos, y+j, zPos] = BlockType.Trunk;
                }

                int leavesWidth = 1 + (int)(rand.NextDouble() * 6);
                int leavesHeight = (int)(rand.NextDouble() * 3);

                int iter = 0;
                for(int m = y + treeHeight - 1; m <= y + treeHeight - 1 + treeHeight; m++)
                {
                    for(int k = xPos - (int)(leavesWidth * .5)+iter/2; k <= xPos + (int)(leavesWidth * .5)-iter/2; k++)
                        for(int l = zPos - (int)(leavesWidth * .5)+iter/2; l <= zPos + (int)(leavesWidth * .5)-iter/2; l++)
                        {
                            if(k >= 0 && k < 16 && l >= 0 && l < 16 && m >= 0 && m < 64 && rand.NextDouble() < .8f)
                                blocks[k, m, l] = BlockType.Leaves;
                        }

                    iter++;
                }


            }
        }
    }


    IEnumerator DelayBuildChunks()
    {
        while(toGenerate.Count > 0)
        {
            BuildChunk(toGenerate[0].x, toGenerate[0].z);
            toGenerate.RemoveAt(0);

            yield return new WaitForSeconds(.2f);

        }

    }


}


public struct ChunkPos
{
    public int x, z;
    public ChunkPos(int x, int z)
    {
        this.x = x;
        this.z = z;
    }
}