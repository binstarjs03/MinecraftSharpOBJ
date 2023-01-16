using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using binstarjs03.AerialOBJ.Core;
using binstarjs03.AerialOBJ.Core.Definitions;
using binstarjs03.AerialOBJ.Core.MinecraftWorld;
using binstarjs03.AerialOBJ.Core.Primitives;
using binstarjs03.AerialOBJ.WpfApp.Components;
using binstarjs03.AerialOBJ.WpfApp.ExtensionMethods;
using binstarjs03.AerialOBJ.WpfApp.Models;
using binstarjs03.AerialOBJ.WpfApp.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace binstarjs03.AerialOBJ.WpfApp.ViewModels;

[ObservableObject]
public partial class ViewportViewModel
{
    private readonly float[] _zoomTable = new float[] { 1, 2, 3, 5, 8, 13, 21, 34 };
    private readonly ILogService _logService;
    private readonly IDefinitionManager _definitionManager;

    // viewport UI states
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRegionTextVisible))]
    private bool _isChunkGridVisible = false;
    [ObservableProperty] private bool _isInfoPanelVisible = false;

    // viewport states
    [ObservableProperty] private Size<int> _screenSize = new(0, 0);
    [ObservableProperty] private Point2Z<float> _cameraPos = Point2Z<float>.Zero;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitMultiplier))]
    [NotifyPropertyChangedFor(nameof(IsRegionTextVisible))]
    private int _zoomLevel = 0;

    [ObservableProperty] private int _heightLevel = 0;
    [ObservableProperty] private int _lowHeightLimit = 0;
    [ObservableProperty] private int _highHeightLimit = 0;

    // mouse states
    [ObservableProperty] private Point2<int> _mouseScreenPos = Point2<int>.Zero;
    [ObservableProperty] private Vector2<int> _mousePosDelta = Vector2<int>.Zero;
    [ObservableProperty] private bool _mouseClickHolding = false;
    [ObservableProperty] private bool _mouseInitClickDrag = true;
    [ObservableProperty] private bool _mouseIsInside = false;

    // mouse block states
    [ObservableProperty] private Point3<int> _mouseBlockCoords = Point3<int>.Zero;
    [ObservableProperty] private Point2Z<int> _mouseChunkCoords = Point2Z<int>.Zero;
    [ObservableProperty] private Point2Z<int> _mouseRegionCoords = Point2Z<int>.Zero;
    [ObservableProperty] private string _mouseBlockName = "";
    [ObservableProperty] private string _mouseBlockDisplayName = "";

    // Region Images
    [ObservableProperty] private ObservableCollection<RegionDataImageModel> _regionDataImageModels = new();

    public ViewportViewModel(GlobalState globalState,
                             IChunkRegionManager chunkRegionManager,
                             ILogService logService,
                             IDefinitionManager definitionManager)
    {
        GlobalState = globalState;
        ChunkRegionManager = chunkRegionManager;
        _definitionManager = definitionManager;
        _logService = logService;

        GlobalState.SavegameLoadInfoChanged += OnGlobalState_SavegameLoadInfoChanged;
        ChunkRegionManager.RegionLoaded += ShowRegionDataImageModel;
        ChunkRegionManager.RegionUnloaded += RemoveRegionDataImageModel;
        ChunkRegionManager.RegionLoadingException += OnRegionLoadingException;
        ChunkRegionManager.ChunkLoadingException += OnChunkLoadingException;
        _definitionManager.ViewportDefinitionChanging += OnDefinitionManager_ViewportDefinitionChanging;
        _definitionManager.ViewportDefinitionChanged += OnDefinitionManager_ViewportDefinitionChanged;
    }

    public GlobalState GlobalState { get; }
    public Func<Size<int>>? GetViewViewportSize { get; set; }
    public float UnitMultiplier => _zoomTable[ZoomLevel];
    public bool IsRegionTextVisible => ZoomLevel == 0 && IsChunkGridVisible;

    public IChunkRegionManager ChunkRegionManager { get; }

    #region Event Handlers

    partial void OnScreenSizeChanged(Size<int> value) => UpdateChunkRegionManager();
    partial void OnCameraPosChanged(Point2Z<float> value) => UpdateChunkRegionManager();
    partial void OnZoomLevelChanged(int value) => UpdateChunkRegionManager();
    partial void OnHeightLevelChanged(int value) => ChunkRegionManager.UpdateHeightLevel(HeightLevel);

    private void OnGlobalState_SavegameLoadInfoChanged(SavegameLoadState state)
    {
        if (state == SavegameLoadState.Opened)
            ReinitializeOnSavegameOpened();
        else if (state == SavegameLoadState.Closed)
            ReinitializeOnSavegameClosed();
        else
            throw new NotImplementedException($"No handler implemented for {nameof(SavegameLoadState)} of {state}");
    }

    private void ShowRegionDataImageModel(RegionDataImageModel regionModel)
    {
        if (GlobalState.HasSavegameLoaded)
            _regionDataImageModels.Add(regionModel);
    }

    private void RemoveRegionDataImageModel(RegionDataImageModel regionModel)
    {
        _regionDataImageModels.Remove(regionModel);
    }

    private void OnRegionLoadingException(Point2Z<int> regionCoords, Exception e)
    {
        _logService.LogException($"Cannot load region {regionCoords}", e);
    }

    private void OnChunkLoadingException(Point2Z<int> chunkCoords, Exception e)
    {
        _logService.LogException($"Cannot load chunk {chunkCoords}", e);
    }

    private void OnDefinitionManager_ViewportDefinitionChanging()
    {
        // We want to request for CRM service to stop so we can safely swap definition.

        // if there is no open savegame, then there is no reason to stop
        // CRM service (current implementation will wait on background thread,
        // which is not run yet so deadblock will occur)
        if (!GlobalState.HasSavegameLoaded)
            return;
        ChunkRegionManager.StopBackgroundThread();
    }

    private void OnDefinitionManager_ViewportDefinitionChanged()
    {
        if (!GlobalState.HasSavegameLoaded)
            return;
        ChunkRegionManager.StartBackgroundThread();
        UpdateChunkRegionManager();
    }

    #endregion Event Handlers

    private void ReinitializeOnSavegameOpened()
    {
        LowHeightLimit = GlobalState.SavegameLoadInfo!.LowHeightLimit;
        HighHeightLimit = GlobalState.SavegameLoadInfo!.HighHeightLimit;
        HeightLevel = HighHeightLimit;

        if (GetViewViewportSize is not null)
            ScreenSize = GetViewViewportSize();

        ChunkRegionManager.StartBackgroundThread();
        ChunkRegionManager.UpdateHeightLevel(HeightLevel);
        UpdateChunkRegionManager();

        if (GlobalState.IsDebugEnabled)
        {
            IsChunkGridVisible = true;
            IsInfoPanelVisible = true;
        }
    }

    private void ReinitializeOnSavegameClosed()
    {
        ChunkRegionManager.Reinitialize();
        CameraPos = new Point2Z<float>(0, 0);
        ZoomLevel = 0;
        ScreenSize = new Size<int>(0, 0);
        LowHeightLimit = 0;
        HighHeightLimit = 0;
        HeightLevel = 0;
        IsChunkGridVisible = false;
        IsInfoPanelVisible = false;
    }

    private void UpdateChunkRegionManager()
    {
        if (GlobalState.HasSavegameLoaded)
            ChunkRegionManager.Update(CameraPos, UnitMultiplier, ScreenSize);
    }

    #region Commands

    [RelayCommand]
    private void OnScreenSizeChanged(SizeChangedEventArgs e)
    {
        Size newSize = e.NewSize;
        ScreenSize = new Size<int>(newSize.Width.Floor(), newSize.Height.Floor());
    }

    [RelayCommand]
    private void OnMouseMove(MouseEventArgs e)
    {
        // check if this is initial mouse click and dragging, we don't want the delta to be very large
        // during initial click & drag, which will cause the viewport to teleporting to somewhere
        (MouseScreenPos, Vector2<int> updatedMousePosDelta) = updateMouseScreenPosAndDelta();
        MousePosDelta = MouseInitClickDrag && MouseClickHolding ? Vector2<int>.Zero : updatedMousePosDelta;
        if (MouseClickHolding)
        {
            Vector2Z<float> cameraPosDelta = new(-MousePosDelta.X / UnitMultiplier, -MousePosDelta.Y / UnitMultiplier);
            CameraPos += cameraPosDelta;
            MouseInitClickDrag = false;
        }
        updateMouseWorldInformation();

        (Point2<int> newMousePos, Vector2<int> newMousePosDelta) updateMouseScreenPosAndDelta()
        {
            Point point = e.GetPosition(e.Source as IInputElement);
            Point2<int> oldMousePos = MouseScreenPos;
            Point2<int> newMousePos = new(point.X.Floor(), point.Y.Floor());
            Vector2<int> newMousePosDelta = newMousePos - oldMousePos;
            return (newMousePos, newMousePosDelta);
        }

        void updateMouseWorldInformation()
        {
            Size<float> floatScreenSize = new(ScreenSize.Width, ScreenSize.Height);
            Point2<float> floatMouseScreenPos = new(MouseScreenPos.X, MouseScreenPos.Y);
            Point2Z<float> mouseWorldPos = PointSpaceConversion.ConvertScreenPosToWorldPos(floatMouseScreenPos, CameraPos, UnitMultiplier, floatScreenSize);
            Point2Z<int> mouseBlockCoords2 = new(MathUtils.Floor(mouseWorldPos.X), MathUtils.Floor(mouseWorldPos.Z));
            Block? block = ChunkRegionManager.GetHighestBlockAt(mouseBlockCoords2);

            MouseChunkCoords = MinecraftWorldMathUtils.GetChunkCoordsAbsFromBlockCoordsAbs(mouseBlockCoords2);
            MouseRegionCoords = MinecraftWorldMathUtils.GetRegionCoordsFromChunkCoordsAbs(MouseChunkCoords);
            if (block is not null)
            {
                MouseBlockCoords = block.Value.Coords;
                MouseBlockName = block.Value.Name;
                if (_definitionManager.CurrentViewportDefinition.BlockDefinitions.TryGetValue(block.Value.Name, out ViewportBlockDefinition? bd))
                    MouseBlockDisplayName = bd.DisplayName;
                else
                    MouseBlockDisplayName = _definitionManager.CurrentViewportDefinition.MissingBlockDefinition.DisplayName;
            }
            else
            {
                MouseBlockCoords = new Point3<int>(mouseBlockCoords2.X, 0, mouseBlockCoords2.Z);
                MouseBlockName = "";
                MouseBlockDisplayName = "Unknown (Unloaded Chunk)";
            }

        }
    }

    [RelayCommand]
    private void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
            ZoomIn();
        else
            ZoomOut();
    }

    [RelayCommand]
    private void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Released)
        {
            MouseClickHolding = false;
            MouseInitClickDrag = true;
        }
    }

    [RelayCommand]
    private void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            MouseClickHolding = true;
    }

    [RelayCommand]
    private void OnMouseEnter()
    {
        MouseIsInside = true;
    }

    [RelayCommand]
    private void OnMouseLeave()
    {
        MouseIsInside = false;
        MouseClickHolding = false;
    }

    [RelayCommand]
    private void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            TranslateCamera(new Point2Z<int>(0, -200));
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            TranslateCamera(new Point2Z<int>(0, 200));
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            TranslateCamera(new Point2Z<int>(-200, 0));
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            TranslateCamera(new Point2Z<int>(200, 0));
            e.Handled = true;
        }
        else if (e.Key == Key.Add)
        {
            ZoomIn();
            e.Handled = true;
        }
        else if (e.Key == Key.Subtract)
        {
            ZoomOut();
            e.Handled = true;
        }
        else
            return;
    }

    private void TranslateCamera(Point2Z<int> direction)
    {
        CameraPos = new Point2Z<float>(CameraPos.X + direction.X / UnitMultiplier, CameraPos.Z + direction.Z / UnitMultiplier);
    }

    private void ZoomIn() => Zoom(ZoomDirection.In);
    private void ZoomOut() => Zoom(ZoomDirection.Out);

    private void Zoom(ZoomDirection direction)
    {
        int newZoomLevel = ZoomLevel;
        if (direction == ZoomDirection.In)
            newZoomLevel++;
        else if (direction == ZoomDirection.Out)
            newZoomLevel--;
        else
            throw new NotImplementedException();
        newZoomLevel = int.Clamp(newZoomLevel, 0, _zoomTable.Length - 1);
        ZoomLevel = newZoomLevel;
    }

    private enum ZoomDirection
    {
        In,
        Out,
    }

    #endregion Commands
}
