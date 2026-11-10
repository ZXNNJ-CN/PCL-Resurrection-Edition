''' <summary>
''' Java 下载页：下载 Mojang 官方 Java 运行时，并展示本机已安装的 Java。
''' </summary>
Public Class PageDownloadJava

    Private IsPageLoaded As Boolean = False

    Private Sub PageDownloadJava_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsPageLoaded Then Return
        IsPageLoaded = True
        RefreshDownloadList()
        RefreshInstalledList()
    End Sub

#Region "下载 Java"

    ''' <summary>
    ''' 获取可下载的 Java 大版本列表并填充下拉框。
    ''' </summary>
    Public Sub RefreshDownloadList()
        ComboJavaVersion.IsEnabled = False
        BtnJavaDownload.IsEnabled = False
        LabJavaListState.Text = "正在获取可下载的 Java 列表……"
        RunInNewThread(
        Sub()
            Try
                Dim Majors = JavaDownloadableMajors()
                RunInUi(
                Sub()
                    ComboJavaVersion.Items.Clear()
                    For Each Pair In Majors
                        ComboJavaVersion.Items.Add(New MyComboBoxItem With {.Content = $"Java {Pair.Key}（{Pair.Value}）", .Tag = Pair.Key})
                    Next
                    If ComboJavaVersion.Items.Count > 0 Then
                        ComboJavaVersion.SelectedIndex = 0
                        ComboJavaVersion.IsEnabled = True
                        BtnJavaDownload.IsEnabled = True
                        LabJavaListState.Text = $"共找到 {Majors.Count} 个可下载的 Java 大版本。下载后存放于 .minecraft\runtime 文件夹。"
                    Else
                        LabJavaListState.Text = "未能获取 Java 列表，请检查网络后点击左侧刷新按钮重试。"
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "获取 Java 下载列表失败")
                RunInUi(Sub() LabJavaListState.Text = "获取 Java 列表失败，请检查网络后重试。")
            End Try
        End Sub, "Java Download List")
    End Sub

    Private Sub BtnJavaDownload_Click(sender As Object, e As EventArgs) Handles BtnJavaDownload.Click
        Dim SelectedItem = TryCast(ComboJavaVersion.SelectedItem, MyComboBoxItem)
        If SelectedItem Is Nothing OrElse SelectedItem.Tag Is Nothing Then
            Hint("请先选择要下载的 Java 版本！", HintType.Red)
            Return
        End If
        Hint($"已开始下载 Java {SelectedItem.Tag}，可在右上角的下载管理中查看进度……", HintType.Blue)
        JavaDownloadManually(CInt(SelectedItem.Tag))
    End Sub

#End Region

#Region "已安装的 Java"

    ''' <summary>
    ''' 列出本机已安装（PCL 已识别）的 Java。
    ''' </summary>
    Public Sub RefreshInstalledList()
        LabJavaInstalledState.Text = "正在获取已安装的 Java……"
        RunInNewThread(
        Sub()
            Try
                JavaListRefreshWorker.Start()
                JavaListRefreshWorker.WaitIfRunning()
                Dim Javas = Configs.JavaList.Get()
                RunInUi(
                Sub()
                    PanJavaInstalled.Children.Clear()
                    If Javas Is Nothing OrElse Javas.Count = 0 Then
                        LabJavaInstalledState.Text = "尚未在本机发现可用的 Java，可以从上方下载官方 Java 运行时。"
                        Return
                    End If
                    LabJavaInstalledState.Text = $"共发现 {Javas.Count} 个可用的 Java："
                    For Each Entry In Javas
                        Dim InfoText = If(Entry.Version Is Nothing, "未知版本", "Java " & Entry.Version.ToString)
                        Dim NewItem As New MyListItem With {
                            .Title = InfoText,
                            .Info = Entry.Folder,
                            .Height = 38,
                            .Type = MyListItem.CheckType.Clickable,
                            .LogoScale = 1.0,
                            .Logo = Logo.IconButtonSave
                        }
                        AddHandler NewItem.Click, Sub() OpenExplorer(Entry.Folder)
                        PanJavaInstalled.Children.Add(NewItem)
                    Next
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "获取已安装 Java 列表失败")
                RunInUi(Sub() LabJavaInstalledState.Text = "获取已安装的 Java 失败。")
            End Try
        End Sub, "Java Installed List")
    End Sub

    Private Sub BtnJavaRescan_Click(sender As Object, e As EventArgs) Handles BtnJavaRescan.Click
        Hint("正在重新扫描本机 Java……", HintType.Blue, Log:=False)
        RefreshInstalledList()
    End Sub

    Private Sub BtnJavaImport_Click(sender As Object, e As EventArgs) Handles BtnJavaImport.Click
        ManuallyImportJava()
        Hint("导入流程已启动，请按照弹窗提示操作。", HintType.Blue, Log:=False)
    End Sub

#End Region

End Class
