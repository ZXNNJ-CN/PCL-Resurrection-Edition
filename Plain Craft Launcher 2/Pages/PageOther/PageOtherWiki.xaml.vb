''' <summary>
''' Wiki 百科页：搜索并跳转中文 Minecraft Wiki。
''' </summary>
Public Class PageOtherWiki

    ''' <summary>常用 Wiki 页面（名称, 页面路径）。</summary>
    Private ReadOnly WikiLinks As New List(Of KeyValuePair(Of String, String)) From {
        New KeyValuePair(Of String, String)("首页（Minecraft Wiki）", "Minecraft_Wiki"),
        New KeyValuePair(Of String, String)("新手入门指南", "教程/新手入门"),
        New KeyValuePair(Of String, String)("合成配方大全", "合成"),
        New KeyValuePair(Of String, String)("命令与指令", "命令"),
        New KeyValuePair(Of String, String)("数据包", "数据包"),
        New KeyValuePair(Of String, String)("资源包", "资源包"),
        New KeyValuePair(Of String, String)("附魔机制", "附魔"),
        New KeyValuePair(Of String, String)("红石基础", "红石电路")
    }

    Private Sub PageOtherWiki_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If PanWikiLinks.Children.Count > 0 Then Return
        For Each Link In WikiLinks
            Dim Name = Link.Key, Page = Link.Value
            Dim NewItem As New MyListItem With {
                .Title = Name,
                .Info = "zh.minecraft.wiki/" & Page,
                .Height = 38,
                .Type = MyListItem.CheckType.Clickable,
                .LogoScale = 1.0,
                .Logo = Logo.IconButtonInfo
            }
            AddHandler NewItem.Click, Sub() OpenWebsite("https://zh.minecraft.wiki/w/" & Page.Replace(" ", "_"))
            PanWikiLinks.Children.Add(NewItem)
        Next
    End Sub

    Private Sub BtnSearch_Click(sender As Object, e As EventArgs) Handles BtnSearch.Click
        Dim Query = TextSearch.Text.Trim
        If Query = "" Then
            Hint("请先输入要搜索的内容！", HintType.Red, Log:=False)
            Return
        End If
        OpenWebsite("https://zh.minecraft.wiki/w/index.php?search=" & Uri.EscapeDataString(Query))
        Hint("已在浏览器中打开搜索结果。", HintType.Blue, Log:=False)
    End Sub

End Class
