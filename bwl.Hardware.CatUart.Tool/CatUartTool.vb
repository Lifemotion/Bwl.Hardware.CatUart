Public Class CatUartTool
    Inherits FormAppBase
    Private _cu As New CatUart(New FastSerialPort())

    Private Sub CatUartTool_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        _cu.SerialDevice.DeviceAddress = "COM11"
        _cu.SerialDevice.DeviceSpeed = 9600
        _cu.SerialDevice.Connect()
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim test = _cu.Request(0, 102, {})
        AppBase.RootLogger.AddMessage(test.ResponseState.ToString)
    End Sub

    Public Sub ErasePage(address As Integer, page As Integer)
        Dim page0 = (page >> 7) And 127
        Dim page1 = page Mod 128
        Dim test = _cu.RequestWithRetries(address, 102, {page0, page1, 0}, 10)
        If test.ResponseState <> ResponseState.ok Then Throw New Exception(test.ResponseState.ToString)
        If test.Response <> 103 Then Throw New Exception(test.ResponseState.ToString)
        AppBase.RootLogger.AddDebug("ErasePage")
    End Sub

    Public Sub FillPageBuffer(address As Integer, page As Integer, offset As Integer, word As Byte())
        If word.Length <> 2 Then Throw New Exception
        Dim page0 = (page >> 7) And 127
        Dim page1 = page Mod 128
        Dim offset0 = (offset >> 7) And 127
        Dim offset1 = offset Mod 128
        Dim word0 = (word(1) >> 7) And 127
        Dim word1 = word(1) Mod 128
        Dim word2 = (word(0) >> 7) And 127
        Dim word3 = word(0) Mod 128
        Dim test = _cu.RequestWithRetries(address, 104, {page0, page1, offset0, offset1, word0, word1, word2, word3}, 10)
        If test.ResponseState <> ResponseState.ok Then Throw New Exception(test.ResponseState.ToString)
        If test.Response <> 105 Then Throw New Exception(test.ResponseState.ToString)

        AppBase.RootLogger.AddDebug("FillPage")
    End Sub

    Public Sub WritePage(address As Integer, page As Integer)
        Dim page0 = (page >> 7) And 127
        Dim page1 = page Mod 128
        Dim test = _cu.RequestWithRetries(address, 106, {page0, page1}, 10)
        If test.ResponseState <> ResponseState.ok Then Throw New Exception(test.ResponseState.ToString)
        If test.Response <> 107 Then Throw New Exception(test.ResponseState.ToString)

        AppBase.RootLogger.AddDebug("Write")
    End Sub

    Public Sub EraseFillWritePage(address As Integer, page As Integer, data As Byte(), offset As Integer, size As Integer)
        If size <> 128 Then Throw New Exception
        If data.Length < offset + size Then Throw New Exception

        Dim buffer(size - 1) As Integer
        Dim datapresent As Boolean
        For i = 0 To buffer.Length - 1
            buffer(i) = data(offset + i)
            ' buffer(i) = i
            If buffer(i) <> 0 And buffer(i) <> 255 Then datapresent = True
        Next
        '   If page = 3 Then Stop
        Debug.WriteLine("EP " + page.ToString)
        ErasePage(address, page)

        If datapresent Then
            For i = 0 To size - 1 Step 2
                FillPageBuffer(address, page, i, {buffer(i), buffer(i + 1)})
            Next
            WritePage(address, page)
        End If
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click

        Dim data(127) As Byte
        For i = 0 To data.Length - 1
            data(i) = i
        Next
        EraseFillWritePage(0, 2, data, 0, 128)
    End Sub



    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim test = _cu.RequestWithRetries(0, 100, {}, 10)
        Dim spm = test.Data(6) * 128 + test.Data(7)
        Dim pgmsize = test.Data(8) * 128 * 128 + test.Data(9) * 128 + test.Data(10)
        Dim sign = test.Data(1) * 256 * 256 + test.Data(3) * 256 + test.Data(5)
        AppBase.RootLogger.AddMessage(test.ResponseState.ToString)

    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Dim spm = 128
        Dim size = (32 - 4) * 1024
        Dim bin = IO.File.ReadAllBytes("C:\Users\Igor\Dropbox\Electronics\Bwl.AwrTest1\Bwl.AwrTest1\Debug\Bwl.AwrTest1.bin")
        ReDim Preserve bin(size - 1) '

        For i = 0 To bin.Length - 1 Step spm
            Dim page As Integer = Math.Floor(i \ spm)
            Debug.WriteLine(i)
            EraseFillWritePage(0, page, bin, i, spm)
        Next
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        Dim test = _cu.RequestWithRetries(0, 108, {115}, 10)

    End Sub
End Class
