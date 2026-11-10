'启动前的 Mod 前置与版本检测：检测与游戏版本不匹配的 Mod 并自动修正，补齐缺失前置，结果在启动时展示
Public Module ModLaunchCheck

    ''' <summary>
    ''' 在启动页显示检测阶段名（此阶段启动加载器尚未启动，否则 UI 会静止在“初始化 0.00%”造成卡死假象）。
    ''' 文本过长会破坏启动页布局，超长部分以省略号截断。
    ''' </summary>
    Private Sub UiStage(Text As String)
        If Text.Length > 26 Then Text = Text.Substring(0, 26) & "…"
        Try
            RunInUi(Sub()
                        FrmLaunchLeft.LabLaunchingStage.Text = Text
                        FrmLaunchLeft.LabLaunchingProgress.Visibility = Visibility.Visible
                        FrmLaunchLeft.LabLaunchingProgressLeft.Visibility = Visibility.Visible
                    End Sub)
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>
    ''' 在启动页显示检测阶段进度（0~1）。
    ''' </summary>
    Private Sub UiProgress(Progress As Double)
        Try
            RunInUi(Sub() FrmLaunchLeft.LabLaunchingProgress.Text = (Math.Max(0, Math.Min(Progress, 1)) * 100).ToString("0.00") & " %")
        Catch ex As Exception
        End Try
    End Sub

    ''' <summary>
    ''' 在启动加载器中执行 Mod 检测。任何异常都不会中断启动。
    ''' </summary>
    Public Sub McLaunchModCheck(Loader As LoaderTask(Of McLaunchOptions, Object))
        Try
            Dim Instance = McInstanceSelected
            If Instance Is Nothing Then Return
            '只检查带 Mod 加载器的版本
            If Not (Instance.Version.ModLoaders.HasFlag(ModLoaders.Forge) OrElse Instance.Version.ModLoaders.HasFlag(ModLoaders.NeoForge) OrElse
                    Instance.Version.ModLoaders.HasFlag(ModLoaders.Fabric) OrElse Instance.Version.ModLoaders.HasFlag(ModLoaders.Quilt)) Then Return
            Dim PathMods As String = Instance.PathIndie & "mods\"
            If Not Directory.Exists(PathMods) Then Return
            Dim ModFiles As List(Of String) = Directory.GetFiles(PathMods, "*.jar").
                Where(Function(f) Not f.ToLower.Contains(".disabled") AndAlso Not f.ToLower.Contains(".old")).ToList()
            If Not ModFiles.Any Then Return
            McLaunchLog($"Mod 检测开始：发现 {ModFiles.Count} 个 Mod 文件")
            UiStage("Mod 检测（识别文件）")
            UiProgress(0.02)

            Dim Mods As New List(Of LocalResourceFile)
            For Each F As String In ModFiles
                Try
                    Mods.Add(New LocalResourceFile(F))
                Catch ex As Exception
                    Logger.Warn(ex, "Mod 检测：无法读取文件 " & F)
                End Try
            Next
            Dim Hashes As List(Of String) = Mods.Select(Function(m) m.ModrinthHash).Where(Function(h) Not String.IsNullOrEmpty(h)).Distinct().ToList()
            If Not Hashes.Any Then
                McLaunchLog("Mod 检测：没有可联网识别的 Mod，跳过")
                Return
            End If

            '步骤 1：哈希 → 本地版本信息（含依赖声明）
            Dim VanillaName As String = Instance.Version.VanillaName
            Dim LoaderFlags As String = Instance.Version.ModLoaders.Flags.Join(""",""").Lower
            Dim HashResp As JObject
            Try
                HashResp = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                    $"{{""hashes"": [""{Hashes.Join(""",""")}""], ""algorithm"": ""sha1""}}", "application/json")
            Catch ex As Exception
                McLaunchLog("Mod 检测：联网查询失败，本次跳过（" & ex.Message & "）", LogLevel.Warn)
                Return
            End Try
            UiProgress(0.05)

            '哈希 → 版本 JSON；建立本地已安装工程集合
            Dim LocalProjects As New HashSet(Of String)
            Dim DepRequired As New List(Of String) '需要的前置工程 ID
            Dim ToFix As New List(Of LocalResourceFile) '版本不匹配的 Mod
            For Each ModEntry As LocalResourceFile In Mods
                If String.IsNullOrEmpty(ModEntry.ModrinthHash) OrElse Not HashResp.ContainsKey(ModEntry.ModrinthHash) Then Continue For
                Dim VerJson As JObject = CType(HashResp(ModEntry.ModrinthHash), JObject)
                Dim ProjectId As String = VerJson("project_id").ToString
                LocalProjects.Add(ProjectId)
                '记录必需前置
                Try
                    If VerJson("dependencies") IsNot Nothing Then
                        For Each Dep As JObject In VerJson("dependencies")
                            Dim DepType As String = If(Dep("dependency_type"), "").ToString
                            If DepType = "optional" Then Continue For
                            Dim DepProjectId As String = If(Dep("project_id"), "").ToString
                            If Not String.IsNullOrEmpty(DepProjectId) AndAlso DepProjectId <> ProjectId AndAlso Not DepRequired.Contains(DepProjectId) Then
                                DepRequired.Add(DepProjectId)
                            End If
                        Next
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "Mod 检测：解析依赖失败")
                End Try
                '版本是否匹配目标游戏版本：向 Modrinth 询问该哈希在目标版本下是否有更新
                ToFix.Add(ModEntry)
            Next

            '步骤 2：请求目标版本下的最新文件（即检测版本不匹配）
            Dim UpdateResp As JObject = Nothing
            If ToFix.Any Then
                Dim AllHashes As List(Of String) = ToFix.Select(Function(m) m.ModrinthHash).Where(Function(h) Not String.IsNullOrEmpty(h)).Distinct().ToList()
                Try
                    UpdateResp = DlModRequest("https://api.modrinth.com/v2/version_files/update", HttpMethod.Post,
                        $"{{""hashes"": [""{AllHashes.Join(""",""")}""], ""algorithm"": ""sha1"", ""loaders"": [""{LoaderFlags}""], ""game_versions"": [""{VanillaName}""]}}", "application/json")
                Catch ex As Exception
                    McLaunchLog("Mod 检测：版本匹配查询失败（" & ex.Message & "）", LogLevel.Warn)
                End Try
            End If

            Dim Fixed As New List(Of String)
            Dim Added As New List(Of String)
            Dim Failed As New List(Of String)

            '步骤 3：下载与目标版本匹配的替换文件（自动修正版本不匹配）
            Dim FixedIndex As Integer = 0
            For Each ModEntry As LocalResourceFile In ToFix.ToList()
                If Loader.IsCanceled Then Return '用户已取消启动
                If UpdateResp Is Nothing OrElse Not UpdateResp.ContainsKey(ModEntry.ModrinthHash) Then
                    '目标版本下没有它的任何版本：无法自动修正，仅提示
                    Continue For
                End If
                Try
                    Dim NewVer As JObject = CType(UpdateResp(ModEntry.ModrinthHash), JObject)
                    Dim NewHash As String = NewVer("files")(0)("hashes")("sha1").ToString
                    If NewHash = ModEntry.ModrinthHash Then Continue For '已是目标版本
                    Dim NewName As String = NewVer("files")(0)("filename").ToString
                    Dim Urls As List(Of String) = New List(Of String) From {NewVer("files")(0)("url").ToString}
                    McLaunchLog($"Mod 检测：{ModEntry.File.Name} 与 {VanillaName} 不匹配，替换为 {NewName}")
                    UiStage($"Mod 检测（下载修正 {FixedIndex + 1}/{ToFix.Count}：{NewName}）")
                    NetDownloadByLoader(Urls, PathMods & NewName)
                    If Loader.IsCanceled Then Return
                    If FileUtils.Exists(PathMods & NewName) Then
                        FileUtils.Delete(ModEntry.File.FullName, True)
                        Fixed.Add($"{ModEntry.File.Name} → {NewName}")
                        '替换后新增的依赖也要检查
                        If NewVer("dependencies") IsNot Nothing Then
                            For Each Dep As JObject In NewVer("dependencies")
                                Dim DepType As String = If(Dep("dependency_type"), "").ToString
                                If DepType = "optional" Then Continue For
                                Dim DepProjectId As String = If(Dep("project_id"), "").ToString
                                If Not String.IsNullOrEmpty(DepProjectId) AndAlso Not DepRequired.Contains(DepProjectId) Then
                                    DepRequired.Add(DepProjectId)
                                End If
                            Next
                        End If
                    Else
                        Failed.Add(ModEntry.File.Name & "（下载失败）")
                    End If
                Catch ex As Exception
                    If Loader.IsCanceled Then Return '取消引发的中断异常不作为检测失败
                    Logger.Warn(ex, "Mod 检测：修正失败 " & ModEntry.File.Name)
                    Failed.Add(ModEntry.File.Name & "（" & ex.Message & "）")
                End Try
                FixedIndex += 1
                UiProgress(Math.Min(0.05 + Fixed.Count * 0.04, 0.4))
            Next
            UiProgress(0.45)

            '步骤 4：补齐缺失的前置
            Dim DepToFetch As List(Of String) = DepRequired.Where(Function(d) Not LocalProjects.Contains(d)).Distinct().ToList()
            For Each DepId As String In DepToFetch
                If Loader.IsCanceled Then Return '用户已取消启动
                Try
                    '获取前置工程信息（确认存在且是 Mod）
                    Dim ProjectJson As JArray = DlModRequest($"https://api.modrinth.com/v2/projects?ids=[""{DepId}""]")
                    If Not ProjectJson.Any Then Continue For
                    Dim DepProject As New ResourceProject(ProjectJson(0))
                    '获取适配目标版本与加载器的版本列表
                    Dim Vers As JArray = DlModRequest(
                        $"https://api.modrinth.com/v2/project/{DepId}/version?game_versions=[""{VanillaName}""]&loaders=[""{LoaderFlags}""]")
                    If Not Vers.Any Then
                        Failed.Add($"缺失前置：{DepProject.RawName}（没有适配 {VanillaName} 的版本）")
                        Continue For
                    End If
                    Dim Best As JObject = CType(Vers(0), JObject)
                    Dim DepName As String = Best("files")(0)("filename").ToString
                    Dim DepUrls As List(Of String) = New List(Of String) From {Best("files")(0)("url").ToString}
                    If FileUtils.Exists(PathMods & DepName) Then Continue For '已存在
                    McLaunchLog($"Mod 检测：补齐前置 {DepProject.RawName} → {DepName}")
                    UiStage($"Mod 检测（下载前置 {Added.Count + 1}：{DepName}）")
                    NetDownloadByLoader(DepUrls, PathMods & DepName)
                    If Loader.IsCanceled Then Return
                    If FileUtils.Exists(PathMods & DepName) Then
                        Added.Add($"{DepProject.RawName}（{DepName}）")
                    Else
                        Failed.Add($"前置下载失败：{DepProject.RawName}")
                    End If
                Catch ex As Exception
                    If Loader.IsCanceled Then Return '取消引发的中断异常不作为检测失败
                    Logger.Warn(ex, "Mod 检测：补前置失败 " & DepId)
                    Failed.Add("前置工程 " & DepId & "（" & ex.Message & "）")
                End Try
                UiProgress(Math.Min(0.45 + Added.Count * 0.04, 0.85))
            Next
            UiProgress(0.9)

            '步骤 5：启动时显示结果
            If Loader.IsCanceled Then Return
            If Fixed.Any OrElse Added.Any OrElse Failed.Any Then
                Dim Lines As New List(Of String)
                If Fixed.Any Then
                    Lines.Add("已修正版本不匹配：" & Fixed.Count & " 个")
                    Lines.AddRange(Fixed.Select(Function(f) "  · " & f))
                End If
                If Added.Any Then
                    Lines.Add("已自动补齐缺失前置：" & Added.Count & " 个")
                    Lines.AddRange(Added.Select(Function(f) "  · " & f))
                End If
                If Failed.Any Then
                    Lines.Add("未能自动处理：" & Failed.Count & " 项")
                    Lines.AddRange(Failed.Select(Function(f) "  · " & f))
                End If
                Dim Summary As String = Lines.Join(Environment.NewLine)
                McLaunchLog("Mod 检测完成：" & Summary.Replace(Environment.NewLine, "；"))
                RunInUiWait(Sub()
                                MyMsgBox($"启动前已完成 Mod 检测：" & Environment.NewLine & Environment.NewLine & Summary &
                                         Environment.NewLine & Environment.NewLine & "（检测结果由 Modrinth 提供）",
                                         "Mod 检测", "继续启动", IsWarn:=Failed.Any)
                            End Sub)
            Else
                McLaunchLog("Mod 检测完成：所有 Mod 均与当前版本匹配，前置齐全")
            End If
            UiProgress(1)
        Catch ex As Exception
            If Loader.IsCanceled Then Return '用户已取消启动，忽略中断期间的一切异常
            Logger.Warn(ex, "Mod 检测出现未处理异常（不影响启动）")
            McLaunchLog("Mod 检测出现异常，已跳过：" & ex.Message, LogLevel.Warn)
        End Try
    End Sub

End Module
