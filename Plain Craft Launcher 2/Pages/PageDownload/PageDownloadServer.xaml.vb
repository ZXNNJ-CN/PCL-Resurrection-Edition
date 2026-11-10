''' <summary>
''' 服务器页：下载原版服务端，并推荐免费公益服务器。
''' </summary>
Public Class PageDownloadServer

    ''' <summary>免费的公益服务器推荐列表（名称, 地址, 说明）。</summary>
    Private ReadOnly ServerRecommendList As New List(Of Tuple(Of String, String, String)) From {
        New Tuple(Of String, String, String)("Hypixel", "mc.hypixel.net", "全球最大的小游戏综合服务器，需正版账号"),
        New Tuple(Of String, String, String)("CubeCraft", "mco.cubecraft.net", "知名小游戏服务器，需正版账号"),
        New Tuple(Of String, String, String)("2B2T", "2b2t.org", "最古老的无政府生存服务器，需正版账号")
    }

    Private IsPageLoaded As Boolean = False

    Private Sub PageDownloadServer_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsPageLoaded Then Return
        IsPageLoaded = True
        RefreshServerRecommend()
        RefreshVersionList()
    End Sub

#Region "免费服务器推荐"

    Private Sub RefreshServerRecommend()
        PanServerRecommend.Children.Clear()
        For Each Entry In ServerRecommendList
            Dim Name = Entry.Item1, Address = Entry.Item2, Desc = Entry.Item3
            Dim NewItem As New MyListItem With {
                .Title = Name,
                .Info = $"{Address}　·　{Desc}",
                .Height = 38,
                .Type = MyListItem.CheckType.Clickable,
                .LogoScale = 1.0,
                .Logo = Logo.IconButtonInfo
            }
            AddHandler NewItem.Click, Sub()
                                          Clipboard.SetText(Address)
                                          Hint($"已复制 {Name} 的服务器地址：{Address}", HintType.Green, Log:=False)
                                      End Sub
            PanServerRecommend.Children.Add(NewItem)
        Next
    End Sub

#End Region

#Region "下载服务端"

    ''' <summary>
    ''' 获取正式版版本列表并填充下拉框。
    ''' </summary>
    Public Sub RefreshVersionList()
        ComboServerVersion.IsEnabled = False
        BtnServerDownload.IsEnabled = False
        LabServerState.Text = "正在获取版本列表……"
        RunInNewThread(
        Sub()
            Try
                Dim Json = NetRequestByLoader(DlVersionListOrder(
                    {"https://launchermeta.mojang.com/mc/game/version_manifest.json"},
                    {"https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"}
                ), IsJson:=True).DeserializeJson(Of JObject)()
                Dim Results As New List(Of KeyValuePair(Of String, String)) '（版本号, 版本 JSON 地址）
                For Each Item As JObject In Json("versions")
                    If Item("type")?.ToString <> "release" Then Continue For
                    Results.Add(New KeyValuePair(Of String, String)(Item("id").ToString, Item("url").ToString))
                Next
                RunInUi(
                Sub()
                    ComboServerVersion.Items.Clear()
                    For Each Pair In Results
                        ComboServerVersion.Items.Add(New MyComboBoxItem With {.Content = Pair.Key, .Tag = Pair.Value})
                    Next
                    If ComboServerVersion.Items.Count > 0 Then
                        ComboServerVersion.SelectedIndex = 0
                        ComboServerVersion.IsEnabled = True
                        BtnServerDownload.IsEnabled = True
                        LabServerState.Text = $"共找到 {Results.Count} 个正式版版本。下载完成后会自动生成 eula.txt（已同意）与 启动服务器.bat。"
                    Else
                        LabServerState.Text = "未能获取版本列表，请检查网络后重试。"
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "获取服务端版本列表失败")
                RunInUi(Sub() LabServerState.Text = "获取版本列表失败，请检查网络后重试。")
            End Try
        End Sub, "Server Version List")
    End Sub

    Private Sub BtnServerDownload_Click(sender As Object, e As EventArgs) Handles BtnServerDownload.Click
        Dim SelectedItem = TryCast(ComboServerVersion.SelectedItem, MyComboBoxItem)
        If SelectedItem Is Nothing OrElse SelectedItem.Tag Is Nothing Then
            Hint("请先选择要下载的游戏版本！", HintType.Red)
            Return
        End If
        Dim VersionId As String = SelectedItem.Content.ToString
        Dim JsonUrl As String = SelectedItem.Tag.ToString
        Dim TargetFolder As String = Dialogs.SelectFolder("选择服务端保存文件夹（建议使用空文件夹）", False).FirstOrDefault
        If String.IsNullOrEmpty(TargetFolder) Then Return
        LabServerState.IsEnabled = False
        LabServerState.Text = $"正在下载 Minecraft {VersionId} 服务端……"
        Hint($"已开始下载 Minecraft {VersionId} 服务端……", HintType.Blue, Log:=False)
        RunInNewThread(
        Sub()
            Try
                ServerDownloadWorker(VersionId, JsonUrl, TargetFolder)
                RunInUi(Sub()
                            LabServerState.IsEnabled = True
                            LabServerState.Text = $"Minecraft {VersionId} 服务端已下载完成！打开服务端文件夹，运行 启动服务器.bat 即可开服。"
                        End Sub)
                Hint($"Minecraft {VersionId} 服务端下载完成！", HintType.Green)
            Catch ex As Exception
                Logger.Error(ex, "下载服务端失败")
                RunInUi(Sub()
                            LabServerState.IsEnabled = True
                            LabServerState.Text = "下载失败：" & ex.Message
                        End Sub)
                Hint("下载服务端失败：" & ex.Message, HintType.Red)
            End Try
        End Sub, "Server Download " & VersionId)
    End Sub

    ''' <summary>
    ''' 执行服务端下载：解析版本 JSON → 下载 server.jar → 写入 eula.txt 与启动脚本。
    ''' </summary>
    Private Sub ServerDownloadWorker(VersionId As String, JsonUrl As String, TargetFolder As String)
        '解析版本 JSON，获取服务端下载地址
        Dim VersionJson As JObject = NetRequestByLoader(DlSourceOrder(
            {JsonUrl},
            {JsonUrl.Replace("launchermeta.mojang.com", "bmclapi2.bangbang93.com").Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com")}
        ), IsJson:=True).DeserializeJson(Of JObject)()
        Dim ServerEntry = VersionJson("downloads")?("server")
        If ServerEntry Is Nothing Then Throw New Exception($"Minecraft {VersionId} 没有提供官方服务端下载。")
        Dim ServerUrl As String = ServerEntry("url").ToString

        '下载 server.jar（官方源 + BMCLAPI 镜像源）
        Dim ServerPath As String = TargetFolder & "server.jar"
        Dim Urls As New List(Of String) From {ServerUrl}
        If ServerUrl.Contains("piston-data.mojang.com") Then Urls.Add(ServerUrl.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com"))
        If ServerUrl.Contains("launchermeta.mojang.com") Then Urls.Add(ServerUrl.Replace("launchermeta.mojang.com", "bmclapi2.bangbang93.com"))
        If ServerUrl.Contains("piston-meta.mojang.com") Then Urls.Add(ServerUrl.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com"))
        NetDownloadByLoader(Urls, ServerPath)

        '写入 eula.txt（视为已同意 Mojang EULA）
        File.WriteAllText(TargetFolder & "eula.txt", "# 此文件由 PCL 生成，保留 eula=true 即表示同意 Minecraft EULA（https://aka.ms/MinecraftEULA）" & vbCrLf & "eula=true" & vbCrLf, Encoding.UTF8)

        '写入启动脚本（仅使用 ASCII，避免编码问题）
        Dim BatContent As String = "@echo off" & vbCrLf &
            "rem Minecraft server startup script generated by PCL" & vbCrLf &
            "rem If the server fails to start, please install a suitable Java first." & vbCrLf &
            "java -Xmx1024M -Xms1024M -jar server.jar nogui" & vbCrLf &
            "pause" & vbCrLf
        File.WriteAllText(TargetFolder & "启动服务器.bat", BatContent, Encoding.ASCII)
        Logger.Info($"服务端 {VersionId} 下载完成：{ServerPath}")
    End Sub

#End Region

End Class
