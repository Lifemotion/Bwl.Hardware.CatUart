Imports bwl.Hardware.Serial
Imports System.Threading
Imports Bwl.Framework

Module Main
    Dim serial1 As ISerialDevice = New FastSerialPort


    Sub Main()

        serial1.DeviceSpeed = 38400
        serial1.DeviceAddress = "COM4"
        serial1.Connect()

        Dim catUart As New CatUart(serial1)
        Dim sec As Integer
        Dim cnt = 0

        Do
            Dim pkt = catUart.Read()
            If sec <> Now.Second Then
                Console.WriteLine("FPS: " + cnt.ToString)
                Console.WriteLine("RBS: " + catUart.ReadBytes.ToString)
                cnt = 0
                catUart.ReadBytes = 0
            End If
            sec = Now.Second

            If pkt IsNot Nothing Then
                cnt += 1
                '  Console.WriteLine(pkt.Data(1))
            End If
            Threading.Thread.Sleep(1)
        Loop


    End Sub

End Module
