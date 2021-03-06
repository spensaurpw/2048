﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class Board : MonoBehaviour {
	[SerializeField] private int rows;
	[SerializeField] private int columns;
	[SerializeField] private float tilePadding;
	[SerializeField] private Image tileBackground;
	[SerializeField] private Transform backgroundRoot;
	[SerializeField] private Transform tileRoot;
	[SerializeField] private Tile tilePrefab;
	[SerializeField] private GameObject gameOverRoot;

	public RectTransform rectTransform;

	private Tile[,] tileGrid;
	private List<Tile> tiles; //to have an enumerable to iterate on
	private Vector2 tileSize;
	private bool slid;
	private bool merged;

	private void Awake() {
		tileGrid = new Tile[columns,rows];
		tiles = new List<Tile>();

		CalculateSizes();

		Application.targetFrameRate = 60;
	}

	private void Start() {
		Messenger.Broadcast<int>("SetPoints",0);

		DrawEmptyTiles();

		//Begin game with two tiles
		SpawnTile();
		SpawnTile();
	}

	private void CalculateSizes() {
		tileSize = rectTransform.rect.size / rows; //Assume it's always square
		tileSize.x -= tilePadding + tilePadding/rows;
		tileSize.y -= tilePadding + tilePadding/columns;
	}

	private void DrawEmptyTiles() {
		for(int x = 0; x < columns; x++) {
			for(int y = 0; y < rows; y++) {
				Image newBG = GameObject.Instantiate(tileBackground, backgroundRoot);
				newBG.name = string.Format("{0},{1}",x,y);
				newBG.rectTransform.sizeDelta = tileSize;
				newBG.rectTransform.localPosition = GetBoardPosition(x,y);

			}
		}
	}

	private Vector3 GetBoardPosition(int x, int y) {
		Vector3 pos = Vector3.zero;

		pos.x = x * tileSize.x + (x+1) * (tilePadding) + tileSize.x/2f;//quick fix after I moved the pivots to the center
		pos.y = y * tileSize.y + (y+1) * (tilePadding) + tileSize.y/2f;

		return pos;
	}

	public void SpawnTile() {
		//First check if board is full in a fast to write slow as hell crappy way
		if(tiles.Count() == rows * columns) {
			Debug.LogWarning("Board::SpawnTile A tile could not be spawned. The board is full");
			return;
		}

		//Find an empty space
		//pick a random starting point
		//if it's occupied iterate through until reaches beginning
		//O(n*m) worst case where 
		int r = Random.Range(0,rows);
		int c = Random.Range(0,columns);

		//This is still pretty bad, but good enough
		if(tileGrid[c,r] != null) {
			for(int y = r+1; y != r; y++) {
				if(y >= rows) { 
					y = -1;
					continue;
				}

				if(tileGrid[c,y] == null) {
					r = y;
					break;
				}
			}

			if(tileGrid[c,r] != null) {
				for(int x = c+1; x != c; x++) {
					if(x >= columns) x = 0;

					for(int y = 0; y < rows; y++) {
						if(tileGrid[x,y] == null) {
							c = x;
							r = y;
							break;
						}
					}

					if(tileGrid[c,r] == null) break;
				}
			}
		}


		Tile newTile = GameObject.Instantiate(tilePrefab, tileRoot);
		newTile.rectTransform.sizeDelta = tileSize;
		newTile.transform.localPosition = GetBoardPosition(c,r);
		newTile.row = r;
		newTile.col = c;
		newTile.SpawnTween();
		tileGrid[c,r] = newTile;
		tiles.Add(newTile);
	}

	private void DoMergeSlide(Tile merging, Tile adjacent) {
		if(CanMerge(merging, adjacent)) {
			merging.SlideTween(GetBoardPosition(adjacent.col,adjacent.row), () => {
				Merge(merging, adjacent);
				});
		} else {
			merging.SlideTween(GetBoardPosition(merging.col, merging.row));
		}
	}

	public void SlideUp() {
		Messenger.Broadcast("DisableInput");

		foreach(Tile t in tiles.OrderByDescending(ti => ti.row)) {
			if(t.row < rows-1) {
				for(int y = t.row; y < rows; y++) {
					if(tileGrid[t.col,y] == null) {
						tileGrid[t.col,t.row] = null;
						tileGrid[t.col,y] = t;
						t.row = y;
						slid = true;
					}
				}

				if(t.row < rows-1) {
					DoMergeSlide(t,tileGrid[t.col,t.row+1]);
				} else {
					t.SlideTween(GetBoardPosition(t.col,t.row));
				}
			}
		}

		StartCoroutine(EndSlide());
	}

	public void SlideDown() {
		Messenger.Broadcast("DisableInput");

		foreach(Tile t in tiles.OrderBy(ti => ti.row)) {
			if(t.row > 0) {
				for(int y = t.row; y >= 0; y--) {
					if(tileGrid[t.col,y] == null) {
						tileGrid[t.col,t.row] = null;
						tileGrid[t.col,y] = t;
						t.row = y;
						slid = true;
					}
				}

				if(t.row > 0) {
					DoMergeSlide(t,tileGrid[t.col,t.row-1]);
				} else {
					t.SlideTween(GetBoardPosition(t.col,t.row));
				}
			}
		}

		StartCoroutine(EndSlide());
	}

	public void SlideLeft() {
		Messenger.Broadcast("DisableInput");

		foreach(Tile t in tiles.OrderBy(ti => ti.col)) {
			if(t.col > 0) {
				for(int x = t.col; x >= 0; x--) {
					if(tileGrid[x,t.row] == null) {
						tileGrid[t.col,t.row] = null;
						tileGrid[x,t.row] = t;
						t.col = x;
						slid = true;
					}
				}

				if(t.col > 0) {
					DoMergeSlide(t,tileGrid[t.col-1,t.row]);
				} else {
					t.SlideTween(GetBoardPosition(t.col,t.row));
				}

			}
		}

		StartCoroutine(EndSlide());
	}

	public void SlideRight() {
		Messenger.Broadcast("DisableInput");

		foreach(Tile t in tiles.OrderByDescending(ti => ti.col)) {
			if(t.col < columns-1) {
				for(int x = t.col; x < columns; x++) {
					if(tileGrid[x,t.row] == null) {
						tileGrid[t.col,t.row] = null;
						tileGrid[x,t.row] = t;
						t.col = x;
						slid = true;
					}
				}

				if(t.col < columns-1) {
					DoMergeSlide(t,tileGrid[t.col+1,t.row]);

				} else {
					t.SlideTween(GetBoardPosition(t.col,t.row));
				}
			}
		}


		StartCoroutine(EndSlide());
	}

	private IEnumerator EndSlide() {
		yield return new WaitForSeconds(Tile.TWEEN_DURATION);

		CheckGameOver();

		if(slid || merged) {
			SpawnTile();
		}

		foreach(Tile t in tiles) {
			t.merging = false;
		}

		slid = false;
		merged = false;

		Messenger.Broadcast("EnableInput");

	}

	private void CheckGameOver() {
		if(tiles.Count() == rows * columns) {
			bool canMerge = false;

			foreach(Tile t in tiles) {
				//check above
				if(t.row < rows - 1) {
					if(tileGrid[t.col,t.row + 1] != null && tileGrid[t.col,t.row + 1].Number == t.Number) {
						canMerge = true;
						break;
					}
				}
				//below
				if(t.row > 0) {
					if(tileGrid[t.col,t.row - 1] != null && tileGrid[t.col,t.row - 1].Number == t.Number) {
						canMerge = true;
						break;
					}
				}
				//left
				if(t.col > 0) {
					if(tileGrid[t.col - 1,t.row] != null && tileGrid[t.col - 1,t.row].Number == t.Number) {
						canMerge = true;
						break;
					}
				}
				//right
				if(t.col < columns - 1) {
					if(tileGrid[t.col + 1,t.row] != null && tileGrid[t.col + 1,t.row].Number == t.Number) {
						canMerge = true;
						break;
					}
				}
			}

			if(!canMerge) {
				gameOverRoot.SetActive(true);
			}
		}
	}

	private void Merge(Tile first, Tile second) {
		second.Number += first.Number;
		second.MergeTween();
		tiles.Remove(first);
		Destroy(first.gameObject);
		merged = true;

		Messenger.Broadcast<int>("AddPoints",second.Number);
	}

	private bool CanMerge(Tile first, Tile second) {
		if(first.Number == second.Number && !first.merging && !second.merging) {
			tileGrid[first.col,first.row] = null;
			second.merging = true;
			return true;
		}

		return false;
	}

	public void TryRestart() {
		if(gameOverRoot.activeInHierarchy) Restart();	
	}

	private void Restart() {
		gameOverRoot.SetActive(false);

		foreach(Tile t in tiles) {
			Destroy(t.gameObject);
		}

		Awake();
		Start();

	}

	#if UNITY_EDITOR

	[ContextMenu("Spawn Tile")]
	public void TestSpawn() {
		SpawnTile();
	}

	[ContextMenu("Redraw")]
	public void RedrawBoard() {
		Restart();
	}

	#endif
}
