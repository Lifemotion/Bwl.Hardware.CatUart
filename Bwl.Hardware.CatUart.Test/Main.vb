Imports bwl.Hardware.Serial
Imports System.Windows.Forms
Imports System.Threading
Module Main
    Dim serial1 As ISerialDevice = New FastSerialPort

    Sub Main()
        '   serial1 = New SerialPort
        SerialVisualiser.CreateAndRunInThread(serial1)
        serial1.DeviceAddress = "COM8"
        serial1.DeviceSpeed = 2400
        serial1.BetweenBytesPause = 0
        serial1.TryConnect()
        Dim catUart As New CatUart(serial1)
        Do
            'Console.ReadLine()
            Try
                Dim t = Now
                'Dim result = catUart.RequestDeviceInfo(16643)
                'catUart.RequestTestDevice(16643)
                Dim res = catUart.Request(16643, 6, {}, 5)
                Dim x, y, z As Integer
                Dim tmp As Single
                x = (res.Data(0) * 128 + res.Data(1) - 8192)
                y = (res.Data(2) * 128 + res.Data(3) - 8192)
                z = (res.Data(4) * 128 + res.Data(5) - 8192)
                tmp = ((res.Data(6) * 128 + res.Data(7) - 8192) / 80) + 10.0
                Console.WriteLine("mag value x: " + x.ToString + " y: " + y.ToString + " z: " + z.ToString + " t: " + tmp.ToString("0.0"))
                If res.ResponseState <> ResponseState.ok Then Throw New Exception(res.ResponseState.ToString)

                Dim msec = (Now - t).TotalMilliseconds
                '  Console.WriteLine("ok " + msec.ToString("0") + " " + ((96 * msec) / 1000).ToString("0.0"))
            Catch ex As Exception
                Console.WriteLine(ex.Message)
            End Try
        Loop
    End Sub

End Module
