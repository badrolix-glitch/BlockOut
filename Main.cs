// содержимое Main.cs
using Godot;
using System.Collections.Generic;

public partial class Main : Node3D
{
	private const int Width = 5;
	private const int Depth = 5;
	private const int Height = 12;

	private static readonly Vector3I[][] Shapes =
	{
		// Плоские фигуры
		new Vector3I[] { new(0, 0, 0), new(1, 0, 0), new(0, 0, 1), new(1, 0, 1) },
		new Vector3I[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(3, 0, 0) },
		new Vector3I[] { new(0, 0, 0), new(0, 0, 1), new(0, 0, 2), new(1, 0, 2) },
		new Vector3I[] { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0), new(1, 0, 1) },
		new Vector3I[] { new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(2, 0, 1) },

		// Объёмные фигуры
		new Vector3I[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) },
		new Vector3I[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 1) },
		new Vector3I[] { new(0, 0, 0), new(0, 1, 0), new(0, 1, 1), new(1, 1, 1) }
	};

	private static readonly Color[] LayerColors =
	{
		new(0.35f, 0.75f, 1.00f),
		new(0.40f, 0.90f, 0.65f),
		new(1.00f, 0.85f, 0.35f),
		new(1.00f, 0.55f, 0.35f),
		new(0.90f, 0.40f, 0.60f),
		new(0.70f, 0.50f, 1.00f),
		new(0.40f, 0.80f, 0.90f),
		new(0.65f, 0.90f, 0.40f),
		new(1.00f, 0.70f, 0.35f),
		new(0.80f, 0.55f, 0.85f),
		new(0.45f, 0.70f, 1.00f),
		new(0.95f, 0.55f, 0.50f)
	};

	private readonly HashSet<Vector3I> _occupied = new();
	private readonly RandomNumberGenerator _rng = new();

	private Node3D _piecesRoot;
	private Node3D _lockedRoot;
	private Node3D _block;
	private Node3D _previewRoot;
	private Vector3I[] _shape;
	private Vector3I[] _nextShape;

	private Label _scoreLabel;
	private Label _levelLabel;
	private Label _gameOverLabel;
	private SubViewport _previewViewport;

	private float _fallTimer;
	private int _score;
	private int _piecesPlaced;
	private bool _gameOver;
	private bool _paused;
	private bool _fastDrop;

	public override void _Ready()
	{
		_rng.Randomize();

		var camera = new Camera3D
		{
			Position = new Vector3(0, 16, 0),
			Current = true
		};
		AddChild(camera);
		camera.LookAt(new Vector3(0, 5, 0), Vector3.Forward);

		var worldEnvironment = new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = Colors.Black
			}
		};
		AddChild(worldEnvironment);


		var light = new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-55, -35, 0),
			LightEnergy = 1.5f
		};
		AddChild(light);

		CreateInterface();

		for (int y = 1; y <= Height; y++)
		{
			AddBar(new Vector3(0, y, -Depth / 2f), new Vector3(Width, 0.02f, 0.02f));
			AddBar(new Vector3(0, y, Depth / 2f), new Vector3(Width, 0.02f, 0.02f));
			AddBar(new Vector3(-Width / 2f, y, 0), new Vector3(0.02f, 0.02f, Depth));
			AddBar(new Vector3(Width / 2f, y, 0), new Vector3(0.02f, 0.02f, Depth));
		}
		
		float guideHeight = Height / 2f;

		// Четыре угловые стойки
		AddBar(new Vector3(-Width / 2f, guideHeight, -Depth / 2f),
			new Vector3(0.02f, Height, 0.02f));
		AddBar(new Vector3(-Width / 2f, guideHeight, Depth / 2f),
			new Vector3(0.02f, Height, 0.02f));
		AddBar(new Vector3(Width / 2f, guideHeight, -Depth / 2f),
			new Vector3(0.02f, Height, 0.02f));
		AddBar(new Vector3(Width / 2f, guideHeight, Depth / 2f),
			new Vector3(0.02f, Height, 0.02f));

		// Вертикальные деления на двух стенках, расположенных вдоль X
		for (int i = 1; i < Width; i++)
		{
			float x = i - Width / 2f;

			AddBar(new Vector3(x, guideHeight, -Depth / 2f),
				new Vector3(0.02f, Height, 0.02f));
			AddBar(new Vector3(x, guideHeight, Depth / 2f),
				new Vector3(0.02f, Height, 0.02f));
		}

		// Вертикальные деления на двух стенках, расположенных вдоль Z
		for (int i = 1; i < Depth; i++)
		{
			float z = i - Depth / 2f;

			AddBar(new Vector3(-Width / 2f, guideHeight, z),
				new Vector3(0.02f, Height, 0.02f));
			AddBar(new Vector3(Width / 2f, guideHeight, z),
				new Vector3(0.02f, Height, 0.02f));
		}

		// Сетка на дне колодца
		for (int i = 1; i < Width; i++)
		{
			float coordinate = i - Width / 2f;

			AddBar(new Vector3(coordinate, 0, 0),
				new Vector3(0.02f, 0.02f, Depth));
			AddBar(new Vector3(0, 0, coordinate),
				new Vector3(Width, 0.02f, 0.02f));
		}
		
		// Рамка чуть внутри стенок и над плоскостью дна
		const float frameEdge = 2.47f;
		const float frameY = 0.03f;
		const float frameLength = 2f * frameEdge;
		const float lineThickness = 0.02f;

		AddBar(new Vector3(0, frameY, -frameEdge),
			new Vector3(frameLength, lineThickness, lineThickness));
		AddBar(new Vector3(0, frameY, frameEdge),
			new Vector3(frameLength, lineThickness, lineThickness));
		AddBar(new Vector3(-frameEdge, frameY, 0),
			new Vector3(lineThickness, lineThickness, frameLength));
		AddBar(new Vector3(frameEdge, frameY, 0),
			new Vector3(lineThickness, lineThickness, frameLength));



		_piecesRoot = new Node3D();
		AddChild(_piecesRoot);

		_lockedRoot = new Node3D();
		_piecesRoot.AddChild(_lockedRoot);

		_nextShape = GetRandomShape();
		UpdateHud();
		SpawnPiece();
	}

	private void CreateInterface()
	{
		var canvas = new CanvasLayer();
		AddChild(canvas);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		canvas.AddChild(center);

		_gameOverLabel = new Label
		{
			Text = "GAME OVER",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_gameOverLabel.AddThemeFontSizeOverride("font_size", 48);
		center.AddChild(_gameOverLabel);

		var panel = new PanelContainer();
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		panel.OffsetLeft = -240;
		panel.OffsetRight = -20;
		panel.OffsetTop = 20;
		panel.OffsetBottom = 430;
		canvas.AddChild(panel);

		var layout = new VBoxContainer();
		panel.AddChild(layout);

		_scoreLabel = new Label { Text = "Score: 0" };
		_scoreLabel.AddThemeFontSizeOverride("font_size", 22);
		layout.AddChild(_scoreLabel);

		_levelLabel = new Label { Text = "Level: 1" };
		_levelLabel.AddThemeFontSizeOverride("font_size", 22);
		layout.AddChild(_levelLabel);

		layout.AddChild(new Label { Text = "NEXT" });

		var previewContainer = new SubViewportContainer
		{
			CustomMinimumSize = new Vector2(190, 190),
			Stretch = true
		};
		layout.AddChild(previewContainer);

		_previewViewport = new SubViewport
		{
			Size = new Vector2I(190, 190),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			OwnWorld3D = true
		};

		previewContainer.AddChild(_previewViewport);

		_previewRoot = new Node3D();
		_previewViewport.AddChild(_previewRoot);

		var previewCamera = new Camera3D
		{
			Position = new Vector3(4, 4, 6),
			Current = true
		};
		_previewRoot.AddChild(previewCamera);
		previewCamera.LookAt(Vector3.Zero, Vector3.Up);

		var previewLight = new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-35, -35, 0),
			LightEnergy = 1.5f
		};
		_previewRoot.AddChild(previewLight);
	}

	private void AddBar(Vector3 position, Vector3 size)
	{
		var bar = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			Position = position,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.1f, 1.0f, 0.25f),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};

		AddChild(bar);
	}


	private Vector3I[] GetRandomShape()
	{
		int index = _rng.RandiRange(0, Shapes.Length - 1);
		return (Vector3I[])Shapes[index].Clone();
	}

	private Color GetLayerColor(int y)
	{
		int index = (y % LayerColors.Length + LayerColors.Length) % LayerColors.Length;
		return LayerColors[index];
	}

	private void AddCube(Node3D parent, Vector3I cell, int layer)
	{
		var cube = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.9f, 0.9f) },
			Position = new Vector3(cell.X, cell.Y, cell.Z),
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = GetLayerColor(layer)
			}
		};

		parent.AddChild(cube);
	}

	private void SpawnPiece()
	{
		_shape = (Vector3I[])_nextShape.Clone();
		_nextShape = GetRandomShape();
		UpdatePreview();

		int minX = int.MaxValue;
		int maxX = int.MinValue;
		int minY = int.MaxValue;
		int maxY = int.MinValue;
		int minZ = int.MaxValue;
		int maxZ = int.MinValue;

		foreach (Vector3I cell in _shape)
		{
			minX = Mathf.Min(minX, cell.X);
			maxX = Mathf.Max(maxX, cell.X);
			minY = Mathf.Min(minY, cell.Y);
			maxY = Mathf.Max(maxY, cell.Y);
			minZ = Mathf.Min(minZ, cell.Z);
			maxZ = Mathf.Max(maxZ, cell.Z);
		}

		int originX = -(minX + maxX) / 2;
		int originZ = -(minZ + maxZ) / 2;
		int originY = Height - 1 - maxY;

		_block = new Node3D
		{
			Position = new Vector3(originX, originY + 0.5f, originZ)
		};
		_piecesRoot.AddChild(_block);

		DrawActivePiece();

		if (!CanPlaceShape(_shape, GetOriginCell()))
		{
			_gameOver = true;
			_gameOverLabel.Text = "GAME OVER";
			_gameOverLabel.Visible = true;
		}
	}

	private void DrawActivePiece()
	{
		foreach (Node child in _block.GetChildren())
		{
			_block.RemoveChild(child);
			child.QueueFree();
		}

		Vector3I origin = GetOriginCell();

		foreach (Vector3I cell in _shape)
		{
			var cube = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.9f, 0.9f) },
				Position = new Vector3(cell.X, cell.Y, cell.Z),
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = GetLayerColor(0)
				}
			};
			_block.AddChild(cube);
		}
	}

	private void UpdatePreview()
	{
		foreach (Node child in _previewRoot.GetChildren())
		{
			if (child is Camera3D || child is DirectionalLight3D)
				continue;

			_previewRoot.RemoveChild(child);
			child.QueueFree();
		}

		float centerX = 0;
		float centerY = 0;
		float centerZ = 0;

		foreach (Vector3I cell in _nextShape)
		{
			centerX += cell.X;
			centerY += cell.Y;
			centerZ += cell.Z;
		}

		centerX /= _nextShape.Length;
		centerY /= _nextShape.Length;
		centerZ /= _nextShape.Length;

		foreach (Vector3I cell in _nextShape)
		{
			var cube = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.8f, 0.8f, 0.8f) },
				Position = new Vector3(cell.X - centerX, cell.Y - centerY, cell.Z - centerZ),
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = GetLayerColor(0)
				}
			};
			_previewRoot.AddChild(cube);
		}
	}

	private Vector3I GetOriginCell()
	{
		return new Vector3I(
			Mathf.RoundToInt(_block.Position.X),
			Mathf.RoundToInt(_block.Position.Y - 0.5f),
			Mathf.RoundToInt(_block.Position.Z)
		);
	}

	private bool CanPlaceShape(Vector3I[] shape, Vector3I origin)
	{
		foreach (Vector3I cell in shape)
		{
			Vector3I target = origin + cell;

			if (target.X < -2 || target.X > 2 ||
				target.Z < -2 || target.Z > 2 ||
				target.Y < 0 || target.Y >= Height ||
				_occupied.Contains(target))
			{
				return false;
			}
		}

		return true;
	}

	private void MoveSideways(Vector3I move)
	{
		Vector3I origin = GetOriginCell();

		if (CanPlaceShape(_shape, origin + move))
			_block.Position += new Vector3(move.X, move.Y, move.Z);
	}

	private void RotatePiece(char axis, bool clockwise)
	{
		Vector3I origin = GetOriginCell();
		Vector3I[] rotated = new Vector3I[_shape.Length];

		for (int i = 0; i < _shape.Length; i++)
		{
			Vector3I cell = _shape[i];

			rotated[i] = axis switch
			{
				'X' when clockwise => new Vector3I(cell.X, cell.Z, -cell.Y),
				'X' => new Vector3I(cell.X, -cell.Z, cell.Y),

				'Y' when clockwise => new Vector3I(-cell.Z, cell.Y, cell.X),
				'Y' => new Vector3I(cell.Z, cell.Y, -cell.X),

				_ when clockwise => new Vector3I(cell.Y, -cell.X, cell.Z),
				_ => new Vector3I(-cell.Y, cell.X, cell.Z)
			};
		}

		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int minZ = int.MaxValue;

		foreach (Vector3I cell in rotated)
		{
			minX = Mathf.Min(minX, cell.X);
			minY = Mathf.Min(minY, cell.Y);
			minZ = Mathf.Min(minZ, cell.Z);
		}

		for (int i = 0; i < rotated.Length; i++)
			rotated[i] -= new Vector3I(minX, minY, minZ);

		Vector3I newOrigin = origin + new Vector3I(minX, minY, minZ);

		if (!CanPlaceShape(rotated, newOrigin))
			return;

		_shape = rotated;
		_block.Position = new Vector3(newOrigin.X, newOrigin.Y + 0.5f, newOrigin.Z);
		DrawActivePiece();
	}

	private void LockPiece()
	{
		Vector3I origin = GetOriginCell();

		foreach (Vector3I cell in _shape)
			_occupied.Add(origin + cell);

		_block.QueueFree();
		_block = null;

		_piecesPlaced++;
		ClearFullLayers();
		RebuildLockedVisuals();
		UpdateHud();
		SpawnPiece();
	}

	private void ClearFullLayers()
	{
		List<int> fullLayers = new();

		for (int y = 0; y < Height; y++)
		{
			bool full = true;

			for (int x = -2; x <= 2 && full; x++)
			{
				for (int z = -2; z <= 2; z++)
				{
					if (!_occupied.Contains(new Vector3I(x, y, z)))
					{
						full = false;
						break;
					}
				}
			}

			if (full)
				fullLayers.Add(y);
		}

		if (fullLayers.Count == 0)
			return;

		int count = fullLayers.Count;
		int points = count switch
		{
			1 => 10,
			2 => 25,
			3 => 50,
			_ => 100 * (1 << (count - 4))
		};
		_score += points;

		HashSet<int> cleared = new(fullLayers);
		HashSet<Vector3I> shifted = new();

		foreach (Vector3I cell in _occupied)
		{
			if (cleared.Contains(cell.Y))
				continue;

			int drop = 0;
			foreach (int layer in fullLayers)
			{
				if (layer < cell.Y)
					drop++;
			}

			shifted.Add(new Vector3I(cell.X, cell.Y - drop, cell.Z));
		}

		_occupied.Clear();
		foreach (Vector3I cell in shifted)
			_occupied.Add(cell);
	}

	private void RebuildLockedVisuals()
	{
		_lockedRoot.QueueFree();
		_lockedRoot = new Node3D();
		_piecesRoot.AddChild(_lockedRoot);

		foreach (Vector3I cell in _occupied)
			AddCube(_lockedRoot, new Vector3I(cell.X, cell.Y + 1, cell.Z), cell.Y);
	}

	private void UpdateHud()
	{
		int level = _piecesPlaced / 10 + 1;
		_scoreLabel.Text = $"Score: {_score}";
		_levelLabel.Text = $"Level: {level}";
	}

	private float GetFallInterval()
	{
		int level = _piecesPlaced / 10;
		return Mathf.Max(0.225f, 0.75f - level * 0.0375f);
	}

	private void RestartGame()
	{
		_piecesRoot.QueueFree();
		_piecesRoot = new Node3D();
		AddChild(_piecesRoot);

		_lockedRoot = new Node3D();
		_piecesRoot.AddChild(_lockedRoot);

		_occupied.Clear();
		_score = 0;
		_piecesPlaced = 0;
		_fallTimer = 0;
		_gameOver = false;
		_paused = false;
		_fastDrop = false;
		_gameOverLabel.Visible = false;

		_nextShape = GetRandomShape();
		UpdateHud();
		SpawnPiece();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey key)
			return;

		if (!key.Pressed)
		{
			if (key.Keycode == Key.Space)
				_fastDrop = false;
			return;
		}

		if (key.Echo)
			return;

		if (_gameOver)
		{
			if (key.Keycode == Key.R)
				RestartGame();
			return;
		}

		if (key.Keycode == Key.P)
		{
			_paused = !_paused;
			_gameOverLabel.Text = "PAUSED";
			_gameOverLabel.Visible = _paused;
			return;
		}

		if (_paused)
			return;

		if (key.Keycode == Key.Space)
		{
			_fastDrop = true;
			return;
		}

		switch (key.Keycode)
		{
			case Key.Left:
				MoveSideways(new Vector3I(-1, 0, 0));
				break;
			case Key.Right:
				MoveSideways(new Vector3I(1, 0, 0));
				break;
			case Key.Up:
				MoveSideways(new Vector3I(0, 0, -1));
				break;
			case Key.Down:
				MoveSideways(new Vector3I(0, 0, 1));
				break;
			case Key.Q:
				RotatePiece('X', true);
				break;
			case Key.W:
				RotatePiece('Y', true);
				break;
			case Key.E:
				RotatePiece('Z', true);
				break;
			case Key.A:
				RotatePiece('X', false);
				break;
			case Key.S:
				RotatePiece('Y', false);
				break;
			case Key.D:
				RotatePiece('Z', false);
				break;
		}
	}

	public override void _Process(double delta)
	{
		if (_block == null || _gameOver || _paused)
			return;

		_fallTimer += (float)delta;

		float interval = _fastDrop ? 0.05f : GetFallInterval();
		if (_fallTimer < interval)
			return;

		_fallTimer = 0f;

		if (CanPlaceShape(_shape, GetOriginCell() + Vector3I.Down))
		{
			_block.Position += Vector3.Down;
			DrawActivePiece();
		}
		else
		{
			LockPiece();
		}
	}
}
