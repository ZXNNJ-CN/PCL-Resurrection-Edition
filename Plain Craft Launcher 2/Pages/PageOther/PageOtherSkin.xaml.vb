'皮肤制作：导入 / 像素编辑 / Steve·Alex 3D 预览 / 导出
Imports System.Windows.Media.Media3D
Imports System.Windows.Media.Imaging
Imports System.Windows.Input

Public Class PageOtherSkin

    '==== 纹理与编辑状态 ====
    Private ReadOnly Texture As New WriteableBitmap(64, 64, 96, 96, PixelFormats.Bgra32, Nothing)
    Private ReadOnly Buf(64 * 64 * 4 - 1) As Byte 'Bgra32：B,G,R,A
    Private Const Zoom As Double = 8
    Private CurrentColor As Color = Color.FromRgb(&HD2, &H69, &H1E)
    Private ToolId As Integer = 0 '0 画笔 1 橡皮 2 取色
    Private ReadOnly UndoStack As New List(Of Byte())
    Private IsPainting As Boolean = False
    Private LastTexel As New Point(-1, -1)
    Private IsLoading As Boolean = True

    '==== 3D 状态 ====
    Private IsSlim As Boolean = False
    Private Yaw As Double = 30, Pitch As Double = 14
    Private PartsGroup As Model3DGroup
    Private RotateGroup As Transform3DGroup
    Private ModelVisual As ModelVisual3D
    Private IsRotating As Boolean = False
    Private LastMouse As Point

#Region "初始化"

    Public Sub New()
        InitializeComponent()
        RectChecker.Fill = CreateCheckerBrush()
        ImgEditor.Source = Texture
        SetupViewport()
        FillBlankSkin()
        BuildPalette()
        UpdateColorUi()
        RefreshTexture()
        IsLoading = False
    End Sub

    Private Function CreateCheckerBrush() As DrawingBrush
        Dim Group As New DrawingGroup
        Group.Children.Add(New GeometryDrawing(New SolidColorBrush(Color.FromRgb(&HEA, &HEA, &HEA)), Nothing,
                                               New RectangleGeometry(New Rect(0, 0, 1, 1))))
        Group.Children.Add(New GeometryDrawing(New SolidColorBrush(Color.FromRgb(&HDC, &HDC, &HDC)), Nothing,
                                               New RectangleGeometry(New Rect(0, 0, 0.5, 0.5))))
        Group.Children.Add(New GeometryDrawing(New SolidColorBrush(Color.FromRgb(&HDC, &HDC, &HDC)), Nothing,
                                               New RectangleGeometry(New Rect(0.5, 0.5, 0.5, 0.5))))
        Return New DrawingBrush With {.Drawing = Group, .TileMode = TileMode.Tile,
            .Viewport = New Rect(0, 0, 16, 16), .ViewportUnits = BrushMappingMode.Absolute}
    End Function

    Private Sub SetupViewport()
        Dim Lights As New Model3DGroup
        Lights.Children.Add(New AmbientLight(Color.FromRgb(215, 215, 215)))
        Lights.Children.Add(New DirectionalLight(Color.FromRgb(80, 80, 80), New Vector3D(-1, -2, -3)))
        Viewport.Children.Add(New ModelVisual3D With {.Content = Lights})
        RotateGroup = New Transform3DGroup()
        Dim RotPitch As New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(1, 0, 0), Pitch))
        Dim RotYaw As New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(0, 1, 0), Yaw))
        RotateGroup.Children.Add(RotPitch)
        RotateGroup.Children.Add(RotYaw)
        PartsGroup = New Model3DGroup With {.Transform = RotateGroup}
        ModelVisual = New ModelVisual3D With {.Content = PartsGroup}
        Viewport.Children.Add(ModelVisual)
        Viewport.Camera = New PerspectiveCamera With {
            .Position = New Point3D(0, 4, 52), .LookDirection = New Vector3D(0, -4, -52),
            .FieldOfView = 38, .UpDirection = New Vector3D(0, 1, 0)}
    End Sub

#End Region

#Region "默认皮肤与像素操作"

    Private Sub SetRect(X As Integer, Y As Integer, W As Integer, H As Integer, C As Color)
        For Py As Integer = Y To Y + H - 1
            For Px As Integer = X To X + W - 1
                Dim Idx = (Py * 64 + Px) * 4
                Buf(Idx) = C.B : Buf(Idx + 1) = C.G : Buf(Idx + 2) = C.R : Buf(Idx + 3) = C.A
            Next
        Next
    End Sub

    ''' <summary>生成一张简洁的默认皮肤（含身体与五官）。</summary>
    Private Sub FillBlankSkin()
        Array.Clear(Buf, 0, Buf.Length)
        Dim Skin As Color = Color.FromRgb(&HC6, &H8C, &H53)
        Dim Hair As Color = Color.FromRgb(&H3B, &H2A, &H1A)
        Dim Shirt As Color = Color.FromRgb(&H1F, &HA9, &HA4)
        Dim Pants As Color = Color.FromRgb(&H3A, &H3A, &HB2)
        Dim EyeWhite As Color = Color.FromRgb(&HF8, &HF8, &HF8)
        Dim EyePupil As Color = Color.FromRgb(&H3C, &H44, &HAA)
        Dim Mouth As Color = Color.FromRgb(&H8A, &H50, &H33)
        '头部底面（8,0~32,8 顶/底，0,8~32,16 四侧）
        SetRect(0, 0, 32, 16, Skin)
        SetRect(8, 0, 8, 8, Hair) '头顶
        SetRect(16, 0, 8, 8, Hair) '头底
        SetRect(0, 8, 8, 8, Hair) '右侧头发
        SetRect(16, 8, 8, 8, Hair) '左侧头发
        SetRect(24, 8, 8, 8, Hair) '后脑勺
        '正面五官（正面在 8,8~16,16）
        SetRect(8, 8, 8, 3, Hair) '刘海
        SetRect(10, 12, 2, 1, EyeWhite)
        SetRect(11, 12, 1, 1, EyePupil)
        SetRect(13, 12, 1, 1, EyeWhite)
        SetRect(12, 12, 1, 1, EyePupil)
        SetRect(12, 13, 2, 1, Mouth)
        '身体（16,16~40,32）
        SetRect(16, 16, 24, 16, Shirt)
        SetRect(28, 16, 8, 4, Pants) '身体底
        '手臂（40,16~56,32）
        SetRect(40, 16, 16, 16, Skin)
        SetRect(44, 16, 4, 4, Shirt)
        SetRect(48, 16, 4, 4, Shirt)
        '腿部（0,16~16,32）
        SetRect(0, 16, 16, 16, Pants)
        SetRect(4, 16, 4, 4, Skin) '腿顶
        SetRect(8, 16, 4, 4, Skin)
        RefreshTexture()
        UndoStack.Clear()
    End Sub

    Private Sub RefreshTexture()
        Texture.WritePixels(New Int32Rect(0, 0, 64, 64), Buf, 64 * 4, 0)
    End Sub

    Private Sub PushUndo()
        UndoStack.Add(CType(Buf.Clone(), Byte()))
        If UndoStack.Count > 40 Then UndoStack.RemoveAt(0)
    End Sub

#End Region

#Region "调色板与颜色"

    Private Sub BuildPalette()
        Dim ColorsHex As String() = {
            "#000000", "#3B3B3B", "#8B8B8B", "#FFFFFF", "#C68C53", "#8A5033", "#5E3A1A", "#3B2A1A",
            "#FF5555", "#FF8B45", "#FFAA00", "#FFFF55", "#55FF55", "#00AA00",
            "#55FFFF", "#00AAAA", "#5555FF", "#0000AA", "#FF55FF", "#AA00AA", "#F0F0F0", "#A0A0A0",
            "#E5C07B", "#98C379"}
        PanPalette.Children.Clear()
        For Each HexStr In ColorsHex
            Dim C As Color = CType(ColorConverter.ConvertFromString(HexStr), Color)
            Dim Swatch As New Border With {
                .Width = 24, .Height = 24, .CornerRadius = New CornerRadius(4),
                .Background = New SolidColorBrush(C), .Margin = New Thickness(0, 0, 6, 6),
                .BorderThickness = New Thickness(1), .BorderBrush = New SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
                .Cursor = Cursors.Hand, .ToolTip = HexStr}
            Dim Captured = C
            AddHandler Swatch.MouseLeftButtonDown, Sub()
                                                       CurrentColor = Captured
                                                       If ToolId = 1 Then SetTool(0)
                                                       UpdateColorUi()
                                                   End Sub
            PanPalette.Children.Add(Swatch)
        Next
    End Sub

    Private Sub UpdateColorUi()
        ColorPreview.Background = New SolidColorBrush(CurrentColor)
        LabColor.Text = $"#{CurrentColor.A:X2}{CurrentColor.R:X2}{CurrentColor.G:X2}{CurrentColor.B:X2}"
    End Sub

    Private Sub SetTool(Id As Integer)
        ToolId = Id
        ToolPen.Checked = (Id = 0)
        ToolEraser.Checked = (Id = 1)
        ToolPick.Checked = (Id = 2)
        PanPaint.Cursor = If(Id = 2, Cursors.Hand, Cursors.Cross)
    End Sub

#End Region

#Region "绘制"

    Private Sub PaintSurface_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles PanPaint.MouseLeftButtonDown
        PanPaint.CaptureMouse()
        PushUndo()
        IsPainting = True
        LastTexel = New Point(-1, -1)
        PaintAt(e.GetPosition(PanPaint))
    End Sub

    Private Sub PaintSurface_MouseMove(sender As Object, e As MouseEventArgs) Handles PanPaint.MouseMove
        If Not IsPainting Then Return
        PaintAt(e.GetPosition(PanPaint))
    End Sub

    Private Sub PaintSurface_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles PanPaint.MouseLeftButtonUp
        IsPainting = False
        PanPaint.ReleaseMouseCapture()
        LastTexel = New Point(-1, -1)
    End Sub

    Private Sub PaintSurface_MouseLeave(sender As Object, e As MouseEventArgs) Handles PanPaint.MouseLeave
        If IsPainting Then LastTexel = New Point(-1, -1)
    End Sub

    Private Sub PaintAt(Pos As Point)
        Dim Tx As Integer = CInt(Math.Floor(Pos.X / Zoom))
        Dim Ty As Integer = CInt(Math.Floor(Pos.Y / Zoom))
        If Tx < 0 OrElse Tx > 63 OrElse Ty < 0 OrElse Ty > 63 Then Return
        If ToolId = 2 Then
            '取色
            Dim Idx = (Ty * 64 + Tx) * 4
            CurrentColor = Color.FromArgb(Buf(Idx + 3), Buf(Idx + 2), Buf(Idx + 1), Buf(Idx))
            UpdateColorUi()
            IsPainting = False
            PanPaint.ReleaseMouseCapture()
            SetTool(0)
            Return
        End If
        '在相邻 texel 之间做线性插值，避免快速拖动出现断点
        Dim FromX As Integer = If(LastTexel.X >= 0, CInt(LastTexel.X), Tx)
        Dim FromY As Integer = If(LastTexel.Y >= 0, CInt(LastTexel.Y), Ty)
        Dim Steps As Integer = Math.Max(Math.Abs(Tx - FromX), Math.Abs(Ty - FromY))
        For StepI As Integer = 0 To Steps
            Dim LX = If(Steps = 0, Tx, FromX + (Tx - FromX) * StepI \ Steps)
            Dim LY = If(Steps = 0, Ty, FromY + (Ty - FromY) * StepI \ Steps)
            If LX < 0 OrElse LX > 63 OrElse LY < 0 OrElse LY > 63 Then Continue For
            Dim Idx = (LY * 64 + LX) * 4
            If ToolId = 1 Then
                Buf(Idx) = 0 : Buf(Idx + 1) = 0 : Buf(Idx + 2) = 0 : Buf(Idx + 3) = 0
            Else
                Buf(Idx) = CurrentColor.B : Buf(Idx + 1) = CurrentColor.G
                Buf(Idx + 2) = CurrentColor.R : Buf(Idx + 3) = 255
            End If
        Next
        LastTexel = New Point(Tx, Ty)
        RefreshTexture()
    End Sub

#End Region

#Region "3D 预览"

    ''' <summary>生成一个带 UV 贴图的方盒网格。UVs 顺序：前、后、右、左、顶、底（图像像素坐标）。</summary>
    Private Function BuildBoxMesh(W As Double, H As Double, D As Double, UVs As Rect()) As MeshGeometry3D
        Dim Mesh As New MeshGeometry3D
        Dim HW = W / 2, HH = H / 2, HD = D / 2
        Dim Faces As New List(Of Point3D()) From {
            New Point3D() {New Point3D(-HW, HH, HD), New Point3D(HW, HH, HD), New Point3D(HW, -HH, HD), New Point3D(-HW, -HH, HD)},
            New Point3D() {New Point3D(HW, HH, -HD), New Point3D(-HW, HH, -HD), New Point3D(-HW, -HH, -HD), New Point3D(HW, -HH, -HD)},
            New Point3D() {New Point3D(HW, HH, HD), New Point3D(HW, HH, -HD), New Point3D(HW, -HH, -HD), New Point3D(HW, -HH, HD)},
            New Point3D() {New Point3D(-HW, HH, -HD), New Point3D(-HW, HH, HD), New Point3D(-HW, -HH, HD), New Point3D(-HW, -HH, -HD)},
            New Point3D() {New Point3D(-HW, HH, -HD), New Point3D(HW, HH, -HD), New Point3D(HW, HH, HD), New Point3D(-HW, HH, HD)},
            New Point3D() {New Point3D(-HW, -HH, HD), New Point3D(HW, -HH, HD), New Point3D(HW, -HH, -HD), New Point3D(-HW, -HH, -HD)}
        }
        For i As Integer = 0 To 5
            Dim R = UVs(i)
            Dim u0 = R.X / 64, v0 = R.Y / 64, u1 = (R.X + R.Width) / 64, v1 = (R.Y + R.Height) / 64
            Dim Tcs As Point() = {New Point(u0, v0), New Point(u1, v0), New Point(u1, v1), New Point(u0, v1)}
            Dim BaseIdx = Mesh.Positions.Count
            For j As Integer = 0 To 3
                Mesh.Positions.Add(Faces(i)(j))
                Mesh.TextureCoordinates.Add(Tcs(j))
            Next
            Mesh.TriangleIndices.Add(BaseIdx)
            Mesh.TriangleIndices.Add(BaseIdx + 1)
            Mesh.TriangleIndices.Add(BaseIdx + 2)
            Mesh.TriangleIndices.Add(BaseIdx)
            Mesh.TriangleIndices.Add(BaseIdx + 2)
            Mesh.TriangleIndices.Add(BaseIdx + 3)
        Next
        Return Mesh
    End Function

    Private Function MakeBox(W As Double, H As Double, D As Double, X As Double, Y As Double, Z As Double, UVs As Rect()) As GeometryModel3D
        Dim Mesh = BuildBoxMesh(W, H, D, UVs)
        Dim Brush As New ImageBrush(Texture) With {
            .Viewbox = New Rect(0, 0, 64, 64), .ViewboxUnits = BrushMappingMode.Absolute,
            .Stretch = Stretch.Fill}
        Dim Mat As New DiffuseMaterial(Brush)
        Dim Model As New GeometryModel3D(Mesh, Mat) With {.BackMaterial = Mat}
        Dim T As New TranslateTransform3D(X, Y, Z)
        Model.Transform = T
        Return Model
    End Function

    ''' <summary>构建整只模型（基础层 + 半透明外层）。位置单位与像素 1:1。</summary>
    Private Sub BuildModel()
        PartsGroup.Children.Clear()
        Dim ArmW As Double = If(IsSlim, 3, 4)
        'UV：前、后、右、左、顶、底
        Dim HeadUV As Rect() = {New Rect(8, 8, 8, 8), New Rect(24, 8, 8, 8), New Rect(0, 8, 8, 8), New Rect(16, 8, 8, 8), New Rect(8, 0, 8, 8), New Rect(16, 0, 8, 8)}
        Dim HeadOvUV As Rect() = {New Rect(40, 8, 8, 8), New Rect(56, 8, 8, 8), New Rect(32, 8, 8, 8), New Rect(48, 8, 8, 8), New Rect(40, 0, 8, 8), New Rect(48, 0, 8, 8)}
        Dim BodyUV As Rect() = {New Rect(20, 20, 8, 12), New Rect(32, 20, 8, 12), New Rect(16, 20, 4, 12), New Rect(28, 20, 4, 12), New Rect(20, 16, 8, 4), New Rect(28, 16, 8, 4)}
        Dim BodyOvUV As Rect() = {New Rect(20, 36, 8, 12), New Rect(32, 36, 8, 12), New Rect(16, 36, 4, 12), New Rect(28, 36, 4, 12), New Rect(20, 32, 8, 4), New Rect(28, 32, 8, 4)}
        Dim ArmBase As Double = If(IsSlim, 46, 44)
        '手臂四侧手工填充（右/左根据宽度计算）
        Dim ArmFront As New Rect(ArmBase, 20, ArmW, 12)
        Dim ArmBack As New Rect(ArmBase + ArmW + 8, 20, ArmW, 12)
        Dim ArmTop As New Rect(ArmBase, 16, ArmW, 4)
        Dim ArmBottom As New Rect(ArmBase + ArmW, 16, ArmW, 4)
        Dim ArmRight As New Rect(40, 20, 4, 12)
        Dim ArmLeft As New Rect(48, 20, 4, 12)
        If IsSlim Then ArmLeft = New Rect(47, 20, 4, 12)
        Dim ArmUVs As Rect() = {ArmFront, ArmBack, ArmRight, ArmLeft, ArmTop, ArmBottom}
        Dim ArmOvFront As New Rect(ArmBase, 36, ArmW, 12)
        Dim ArmOvBack As New Rect(ArmBase + ArmW + 8, 36, ArmW, 12)
        Dim ArmOvTop As New Rect(ArmBase, 32, ArmW, 4)
        Dim ArmOvBottom As New Rect(ArmBase + ArmW, 32, ArmW, 4)
        Dim ArmOvRight As New Rect(40, 36, 4, 12)
        Dim ArmOvLeft As New Rect(48, 36, 4, 12)
        Dim ArmOvUVs As Rect() = {ArmOvFront, ArmOvBack, ArmOvRight, ArmOvLeft, ArmOvTop, ArmOvBottom}
        Dim LegUV As Rect() = {New Rect(4, 20, 4, 12), New Rect(12, 20, 4, 12), New Rect(0, 20, 4, 12), New Rect(8, 20, 4, 12), New Rect(4, 16, 4, 4), New Rect(8, 16, 4, 4)}
        Dim LegOvUV As Rect() = {New Rect(4, 36, 4, 12), New Rect(12, 36, 4, 12), New Rect(0, 36, 4, 12), New Rect(8, 36, 4, 12), New Rect(4, 32, 4, 4), New Rect(8, 32, 4, 4)}

        '头（中心 y=10），外层头套
        AddBox(8, 8, 8, 0, 10, 0, HeadUV)
        AddBox(9, 9, 9, 0, 10, 0, HeadOvUV)
        '身体（中心 y=0）
        AddBox(8, 12, 4, 0, 0, 0, BodyUV)
        AddBox(8.5, 12.5, 4.5, 0, 0, 0, BodyOvUV)
        '左右臂（中心 y=0）
        Dim ArmX As Double = 4 + ArmW / 2
        AddBox(ArmW, 12, 4, -ArmX, 0, 0, ArmUVs)
        AddBox(ArmW, 12, 4, ArmX, 0, 0, ArmUVs)
        AddBox(ArmW + 0.5, 12.5, 4.5, -ArmX, 0, 0, ArmOvUVs)
        AddBox(ArmW + 0.5, 12.5, 4.5, ArmX, 0, 0, ArmOvUVs)
        '左右腿（中心 y=-12）
        AddBox(4, 12, 4, -2, -12, 0, LegUV)
        AddBox(4, 12, 4, 2, -12, 0, LegUV)
        AddBox(4.5, 12.5, 4.5, -2, -12, 0, LegOvUV)
        AddBox(4.5, 12.5, 4.5, 2, -12, 0, LegOvUV)
    End Sub

    Private Sub AddBox(W As Double, H As Double, D As Double, X As Double, Y As Double, Z As Double, UVs As Rect())
        PartsGroup.Children.Add(MakeBox(W, H, D, X, Y, Z, UVs))
    End Sub

    Private Sub UpdateRotate()
        CType(RotateGroup.Children(0), RotateTransform3D).Rotation = New AxisAngleRotation3D(New Vector3D(1, 0, 0), Pitch)
        CType(RotateGroup.Children(1), RotateTransform3D).Rotation = New AxisAngleRotation3D(New Vector3D(0, 1, 0), Yaw)
    End Sub

    Private Sub ViewportBorder_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles ViewportBorder.MouseLeftButtonDown
        ViewportBorder.CaptureMouse()
        IsRotating = True
        LastMouse = e.GetPosition(ViewportBorder)
    End Sub

    Private Sub ViewportBorder_MouseMove(sender As Object, e As MouseEventArgs) Handles ViewportBorder.MouseMove
        If Not IsRotating Then Return
        Dim Pos = e.GetPosition(ViewportBorder)
        Yaw += (Pos.X - LastMouse.X) * 0.5
        Pitch = Math.Max(-89, Math.Min(89, Pitch + (Pos.Y - LastMouse.Y) * 0.5))
        LastMouse = Pos
        UpdateRotate()
    End Sub

    Private Sub ViewportBorder_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles ViewportBorder.MouseLeftButtonUp
        IsRotating = False
        ViewportBorder.ReleaseMouseCapture()
    End Sub

#End Region

#Region "按钮与交互"

    Private Sub BtnNew_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNew.Click
        FillBlankSkin()
        Hint("已生成默认皮肤", HintType.Green)
    End Sub

    Private Sub BtnUndo_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnUndo.Click
        If UndoStack.Count = 0 Then
            Hint("没有可撤销的操作", HintType.Blue)
            Return
        End If
        Dim Last = UndoStack(UndoStack.Count - 1)
        UndoStack.RemoveAt(UndoStack.Count - 1)
        Array.Copy(Last, Buf, Buf.Length)
        RefreshTexture()
    End Sub

    Private Sub BtnImport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnImport.Click
        Dim FileName As String = Dialogs.SelectFile("选择皮肤文件", False, filter:={({"png"}, "PNG 皮肤图片")}).FirstOrDefault()
        If String.IsNullOrEmpty(FileName) Then Return
        Try
            Dim Img As New BitmapImage()
            Img.BeginInit()
            Img.CacheOption = BitmapCacheOption.OnLoad
            Img.UriSource = New Uri(FileName)
            Img.EndInit()
            Img.Freeze()
            If Img.PixelWidth = 64 AndAlso Img.PixelHeight = 64 Then
                Img.CopyPixels(Buf, 64 * 4, 0)
            ElseIf Img.PixelWidth = 64 AndAlso Img.PixelHeight = 32 Then
                '旧版 64x32 皮肤转换为 64x64：复制本体并镜像生成左臂/左腿
                Dim OldBuf(64 * 32 * 4 - 1) As Byte
                Img.CopyPixels(OldBuf, 64 * 4, 0)
                Array.Clear(Buf, 0, Buf.Length)
                For Row As Integer = 0 To 31
                    For Col As Integer = 0 To 63
                        Dim SrcIdx = (Row * 64 + Col) * 4
                        Dim DstIdx = (Row * 64 + Col) * 4
                        Buf(DstIdx) = OldBuf(SrcIdx) : Buf(DstIdx + 1) = OldBuf(SrcIdx + 1)
                        Buf(DstIdx + 2) = OldBuf(SrcIdx + 2) : Buf(DstIdx + 3) = OldBuf(SrcIdx + 3)
                    Next
                Next
                CopyMirrorRegion(40, 16, 16, 16, 32, 48) '右臂 → 左臂
                CopyMirrorRegion(0, 16, 16, 16, 0, 48)   '右腿 → 左腿
            Else
                MyMsgBox("皮肤图片必须是 64x64（或旧版 64x32）的 PNG。" & vbCrLf & $"当前图片为 {Img.PixelWidth}x{Img.PixelHeight}。", "导入失败", "确定", IsWarn:=True)
                Return
            End If
            RefreshTexture()
            UndoStack.Clear()
            Hint("皮肤导入成功", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "导入皮肤失败")
            MyMsgBox("导入皮肤失败：" & ex.Message, "导入失败", "确定", IsWarn:=True)
        End Try
    End Sub

    ''' <summary>把 16x16 的肢体区域水平镜像复制到目标位置（64x32 → 64x64 转换用）。</summary>
    Private Sub CopyMirrorRegion(SrcX As Integer, SrcY As Integer, W As Integer, H As Integer, DstX As Integer, DstY As Integer)
        For Row As Integer = 0 To H - 1
            For Col As Integer = 0 To W - 1
                '每 4 像素一个面组，面组内水平镜像
                Dim SrcCol As Integer = SrcX + (Col \ 4) * 4 + (3 - Col Mod 4)
                Dim SrcIdx = ((SrcY + Row) * 64 + SrcCol) * 4
                Dim DstIdx = ((DstY + Row) * 64 + DstX + Col) * 4
                Buf(DstIdx) = Buf(SrcIdx) : Buf(DstIdx + 1) = Buf(SrcIdx + 1)
                Buf(DstIdx + 2) = Buf(SrcIdx + 2) : Buf(DstIdx + 3) = Buf(SrcIdx + 3)
            Next
        Next
    End Sub

    Private Sub BtnExport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnExport.Click
        Dim Target As String = Dialogs.SaveFile("导出皮肤", "skin.png", filter:={("png", "PNG 皮肤图片")})
        If String.IsNullOrEmpty(Target) Then Return
        If Not Target.ToLower.EndsWith(".png") Then Target &= ".png"
        Try
            Dim Encoder As New PngBitmapEncoder()
            Encoder.Frames.Add(BitmapFrame.Create(Texture))
            Using Fs As New IO.FileStream(Target, IO.FileMode.Create)
                Encoder.Save(Fs)
            End Using
            Hint($"已导出：{Target}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "导出皮肤失败")
            MyMsgBox("导出皮肤失败：" & ex.Message, "导出失败", "确定", IsWarn:=True)
        End Try
    End Sub

    Private Sub BtnSetColor_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSetColor.Click
        Try
            Dim Text As String = TextHex.Text.Trim()
            If Not Text.StartsWith("#") Then Text = "#" & Text
            CurrentColor = CType(ColorConverter.ConvertFromString(Text), Color)
            UpdateColorUi()
            If ToolId = 1 Then SetTool(0)
        Catch ex As Exception
            Hint("颜色格式无效，请输入如 #FF8B4514 的格式", HintType.Red)
        End Try
    End Sub

    Private Sub ToolRadio_Check(sender As Object, raiseByMouse As Boolean) Handles ToolPen.Check, ToolEraser.Check, ToolPick.Check
        If IsLoading Then Return
        If sender Is ToolPen Then
            ToolId = 0
        ElseIf sender Is ToolEraser Then
            ToolId = 1
        ElseIf sender Is ToolPick Then
            ToolId = 2
        End If
        PanPaint.Cursor = If(ToolId = 2, Cursors.Hand, Cursors.Cross)
    End Sub

    Private Sub ModelRadio_Check(sender As Object, raiseByMouse As Boolean) Handles RadioSteve.Check, RadioAlex.Check
        If IsLoading Then Return
        Dim NewSlim As Boolean = sender Is RadioAlex
        If NewSlim = IsSlim Then Return
        IsSlim = NewSlim
        BuildModel()
    End Sub

    Private Sub BtnResetView_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnResetView.Click
        Yaw = 30 : Pitch = 14
        UpdateRotate()
    End Sub

#End Region

End Class
