Imports bwl.Hardware.Serial
Imports System.Threading
Imports Bwl.Framework

Module Main
    Dim serial1 As ISerialDevice = New FastSerialPort


    Sub Main()

        Dim catUart As New CatUart(serial1)
        serial1.DeviceSpeed = 38400
        serial1.DeviceAddress = "COM7"
        serial1.Connect()
        '  Do
        '     Console.ReadLine()

        'catUart.SendPacket(0, 33, {1, 2, 3, 4}, 1)
        '   catUart.SendPacket(0, 33, {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24}, 1)
        '   Console.WriteLine(1)

        '    Dim pkt = catUart.Read()
        '    If pkt IsNot Nothing Then
        '    Beep()
        '  Console.WriteLine(pkt.Data(1))
        '    End If

        '   Loop
        '
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
