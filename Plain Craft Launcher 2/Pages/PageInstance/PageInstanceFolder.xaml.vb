''' <summary>
''' 版本游戏资源通用页：展示并管理当前版本的存档、截图、资源包、光影包、投影原理图与服务器列表。
''' </summary>
Public Class PageInstanceFolder

#Region "种类定义"

    Public Enum FolderKind
        Saves = 0
        Screenshots = 1
        ResourcePacks = 2
        ShaderPacks = 3
        Schematics = 4
        Servers = 5
    End Enum

    ''' <summary>当前展示的资源种类。</summary>
    Public Kind As FolderKind = FolderKind.Saves

    ''' <summary>
    ''' 切换展示的资源种类（由版本左侧菜单调用）。
    ''' </summary>
    Public Sub SetKind(NewKind As FolderKind)
        Kind = NewKind
        RefreshContent()
    End Sub

#End Region

    Private Sub PageInstanceFolder_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RefreshContent()
    End Sub

    ''' <summary>获取该种类对应的文件夹路径与说明文本。</summary>
    Private Sub GetFolderInfo(ByRef Folder As String, ByRef Title As String, ByRef Desc As String)
        Dim Root As String = PageInstanceLeft.Instance.PathIndie
        Select Case Kind
            Case FolderKind.Saves
                Folder = Root & "saves\"
                Title = "存档"
                Desc = "双击条目可以打开对应的存档文件夹。"
            Case FolderKind.Screenshots
                Folder = Root & "screenshots\"
                Title = "截图"
                Desc = "双击条目可以用看图软件打开截图。"
            Case FolderKind.ResourcePacks
                Folder = Root & "resourcepacks\"
                Title = "资源包"
                Desc = "双击条目可以打开资源包（未解压的 zip 会用压缩软件打开）。"
            Case FolderKind.ShaderPacks
                Folder = Root & "shaderpacks\"
                Title = "光影包"
                Desc = "双击条目可以打开光影包文件。"
            Case FolderKind.Schematics
                Title = "投影原理图"
                Folder = If(Directory.Exists(Root & "litematics\"), Root & "litematics\", Root & "schematics\")
                Desc = "包含 Litematica（投影）或 WorldEdit 的原理图文件，双击条目可以打开。"
            Case Else
                Title = "服务器"
                Folder = Root
                Desc = "双击 servers.dat 可以用 NBT 编辑器打开并管理服务器列表。"
        End Select
    End Sub

    ''' <summary>
    ''' 根据当前种类刷新列表。
    ''' </summary>
    Public Sub RefreshContent()
        If PageInstanceLeft.Instance Is Nothing Then Return
        Try
            PanList.Children.Clear()
            Dim Folder As String = "", Title As String = "", Desc As String = ""
            GetFolderInfo(Folder, Title, Desc)
            CardFolder.Title = Title
            LabDesc.Text = Desc

            '收集条目（显示名, 完整路径, 是否文件夹）
            Dim Entries As New List(Of Tuple(Of String, String, Boolean))
            If Kind = FolderKind.Servers Then
                '服务器列表：展示 servers.dat 与 server-resource-packs 文件夹
                Dim DatFile As String = Folder & "servers.dat"
                If FileUtils.Exists(DatFile) Then Entries.Add(New Tuple(Of String, String, Boolean)("servers.dat（服务器列表存档）", DatFile, False))
                Dim SrpFolder As String = Folder & "server-resource-packs\"
                If Directory.Exists(SrpFolder) Then Entries.Add(New Tuple(Of String, String, Boolean)("server-resource-packs（服务器资源包缓存）", SrpFolder, True))
            Else
                DirectoryUtils.Create(Folder)
                If Kind = FolderKind.Saves Then
                    For Each SubDir In Directory.GetDirectories(Folder)
                        Entries.Add(New Tuple(Of String, String, Boolean)(Path.GetFileName(SubDir), SubDir, True))
                    Next
                Else
                    For Each SubFile In Directory.GetFiles(Folder)
                        Entries.Add(New Tuple(Of String, String, Boolean)(Path.GetFileName(SubFile), SubFile, False))
                    Next
                    '资源包/光影包也可能以文件夹形式解压存放
                    If Kind = FolderKind.ResourcePacks OrElse Kind = FolderKind.ShaderPacks Then
                        For Each SubDir In Directory.GetDirectories(Folder)
                            Entries.Add(New Tuple(Of String, String, Boolean)(Path.GetFileName(SubDir), SubDir, True))
                        Next
                    End If
                End If
                Entries.Sort(Function(A, B) String.Compare(A.Item1, B.Item1, StringComparison.OrdinalIgnoreCase))
            End If

            '填充 UI
            If Not Entries.Any Then
                LabDesc.Text = Desc & vbCrLf & "该文件夹目前是空的。"
                Return
            End If
            For Each Entry In Entries
                Dim ShowName = Entry.Item1, FullPath = Entry.Item2, IsFolder = Entry.Item3
                Dim SizeText As String = ""
                If Not IsFolder Then
                    Try
                        Dim Len As Long = New FileInfo(FullPath).Length
                        SizeText = "　·　" & If(Len >= 1024 * 1024, Math.Round(Len / 1024 / 1024, 1) & " MB", Math.Round(Len / 1024, 1) & " KB")
                    Catch
                    End Try
                End If
                Dim NewItem As New MyListItem With {
                    .Title = ShowName,
                    .Info = If(IsFolder, "文件夹", "文件") & SizeText & "　·　" & FullPath,
                    .Height = 38,
                    .Type = MyListItem.CheckType.Clickable,
                    .LogoScale = 1.0,
                    .Logo = If(IsFolder, Logo.IconButtonSetup, Logo.IconButtonSave)
                }
                AddHandler NewItem.Click, Sub()
                                              Try
                                                  OpenExplorer(FullPath)
                                              Catch ex As Exception
                                                  Logger.Warn(ex, "打开文件失败")
                                              End Try
                                          End Sub
                PanList.Children.Add(NewItem)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取游戏资源列表失败")
        End Try
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As EventArgs) Handles BtnOpenFolder.Click
        If PageInstanceLeft.Instance Is Nothing Then Return
        Dim Folder As String = "", Title As String = "", Desc As String = ""
        GetFolderInfo(Folder, Title, Desc)
        If Kind = FolderKind.Servers Then
            OpenExplorer(PageInstanceLeft.Instance.PathIndie)
        Else
            DirectoryUtils.Create(Folder)
            OpenExplorer(Folder)
        End If
    End Sub

    Private Sub BtnRefreshList_Click(sender As Object, e As EventArgs) Handles BtnRefreshList.Click
        RefreshContent()
        Hint("已刷新列表。", HintType.Blue, Log:=False)
    End Sub

End Class
