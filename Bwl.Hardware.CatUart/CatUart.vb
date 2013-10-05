Imports bwl.Hardware.Serial
Public Class CatUart
    Private _serial As ISerialDevice
    Private _lastPacket As Request
    Public Sub New(serial As ISerialDevice)
        _serial = serial
        _serial.AutoReadBytes = False
    End Sub

    Public ReadOnly Property SerialDevice
        Get
            Return _serial
        End Get
    End Property

    Protected Sub FitToByte(ByRef data As Integer)
        If data < 0 Then data = 0
        If data > 255 Then data = 255
        Dim tmp As Byte = data
        tmp = tmp And 127
        data = tmp
    End Sub

    Protected Sub SendPacket(packet As Request)
        SendPacket(packet.Address, packet.Command, packet.Data)
    End Sub

    Protected Sub SendPacket(address As Integer, command As Integer, data1 As Integer())
        SendPacket08(address, command, data1)
    End Sub

    Protected Sub SendPacket08(address As Integer, command As Integer, data1 As Integer())
        Dim data = data1.Clone
        ReDim Preserve data(32)
        FitToByte(command)
        FitToByte(data(0))
        FitToByte(data(1))
        FitToByte(data(2))
        FitToByte(data(3))
        Dim type As Byte = &H8
        Dim bytes(12) As Byte
        bytes(0) = 0
        bytes(1) = &HFE
        bytes(2) = address \ 128 \ 128
        bytes(3) = (address \ 128) Mod 128
        bytes(4) = address Mod 128
        bytes(5) = type
        bytes(6) = command
        bytes(7) = data(0)
        bytes(8) = data(1)
        bytes(9) = data(2)
        bytes(10) = data(3)
        bytes(11) = Crc8.ComputeCrc(170, bytes, 2, 10)
        bytes(12) = &HFC
        FitToByte(bytes(11))
        _serial.Write(bytes)
    End Sub

    Protected Sub SendPacket05(address As Integer, command As Integer, data1 As Integer())
        Dim data = data1.Clone
        ReDim Preserve data(32)
        FitToByte(command)
        Dim type As Byte = &H5
        Dim bytes(8) As Byte
        bytes(0) = 0
        bytes(1) = &HFE
        bytes(2) = address \ 128 \ 128
        bytes(3) = (address \ 128) Mod 128
        bytes(4) = address Mod 128
        bytes(5) = type
        bytes(6) = command
        bytes(7) = Crc8.ComputeCrc(170, bytes, 2, 6)
        bytes(8) = &HFC
        FitToByte(bytes(7))
        _serial.Write(bytes)
    End Sub

    Public Property RequestTimeout As Integer = 1000

    Public Function Request(req As Request) As Response
        Return Request(req.Address, req.Command, req.Data)
    End Function

    Public Function Request(address As Integer, command As Integer, dataReq As Integer(), Optional type As Integer = 8) As Response
        Dim result As New Response
        SyncLock _serial
            Do While _serial.ReceivedBufferCount > 0
                _serial.Read()
            Loop
            Select Case type
                Case 8
                    SendPacket08(address, command, dataReq)
                Case 5
                    SendPacket05(address, command, dataReq)
            End Select
            result.ResponseState = ResponseState.errorTimeout
            Dim time = Now
            Dim receivedLength As Integer
            Dim receivedBuffer(32) As Byte

            Do While (Now - time).TotalMilliseconds < RequestTimeout And result.ResponseState = ResponseState.errorTimeout
                If _serial.ReceivedBufferCount > 0 Then
                    Dim data = _serial.Read()
                    Select Case data
                        Case &HFD
                            receivedLength = 0
                        Case &HFB
                            result.Address = receivedBuffer(0) * 128 * 128 + receivedBuffer(1) * 128 + receivedBuffer(2)
                            result.Type = receivedBuffer(3)
                            If result.Type = 8 Then
                                If receivedLength = 10 Then
                                    result.Response = receivedBuffer(4)
                                    result.Data(0) = receivedBuffer(5)
                                    result.Data(1) = receivedBuffer(6)
                                    result.Data(2) = receivedBuffer(7)
                                    result.Data(3) = receivedBuffer(8)
                                    Dim crc1 = receivedBuffer(9)
                                    Dim crc1real = Crc8.ComputeCrc(&HAA, receivedBuffer, 0, 8) Mod 128
                                    If crc1 = crc1real Then
                                        result.ResponseState = ResponseState.ok
                                    Else
                                        result.ResponseState = ResponseState.errorCrc
                                    End If
                                Else
                                    result.ResponseState = ResponseState.errorFormat
                                End If
                            ElseIf result.Type = 10 Then
                                If receivedLength = 14 Then
                                    result.Response = receivedBuffer(4)
                                    result.Data(0) = receivedBuffer(5)
                                    result.Data(1) = receivedBuffer(6)
                                    result.Data(2) = receivedBuffer(7)
                                    result.Data(3) = receivedBuffer(8)
                                    result.Data(4) = receivedBuffer(9)
                                    result.Data(5) = receivedBuffer(10)
                                    result.Data(6) = receivedBuffer(11)
                                    result.Data(7) = receivedBuffer(12)
                                    Dim crc1 = receivedBuffer(13)
                                    Dim crc1real = Crc8.ComputeCrc(&HAA, receivedBuffer, 0, 12) And 127
                                    If crc1 = crc1real Then
                                        result.ResponseState = ResponseState.ok
                                    Else
                                        result.ResponseState = ResponseState.errorCrc
                                    End If
                                Else
                                    result.ResponseState = ResponseState.errorFormat
                                End If
                            Else
                                result.ResponseState = ResponseState.errorPacketType
                            End If
                        Case Else
                            If receivedLength < 32 Then
                                receivedBuffer(receivedLength) = data
                                receivedLength += 1
                            End If
                    End Select
                Else
                    Threading.Thread.Sleep(10)
                End If
            Loop
            Return result
        End SyncLock
    End Function

    Public Function QuickRequest(address As Integer) As Integer
        Throw New Exception("Not implemented")
    End Function

    Public Sub RequestTestDevice(address As Integer)
        Dim req As New Request
        Dim rnd As New Random
        req.Address = address
        req.Command = 122
        req.Data(0) = rnd.Next(1, 126)
        req.Data(1) = rnd.Next(1, 126)
        req.Data(2) = rnd.Next(1, 126)
        req.Data(3) = rnd.Next(1, 126)
        Dim result = Request(req)
        If result.ResponseState = ResponseState.ok Then
            If result.Response <> 123 Then Throw New Exception("RequestTestDevice WrongAnswerData")
            If result.Data(0) <> req.Data(0) + 1 Then Throw New Exception("RequestTestDevice WrongAnswerData")
            If result.Data(1) <> req.Data(1) + 1 Then Throw New Exception("RequestTestDevice WrongAnswerData")
            If result.Data(2) <> req.Data(2) + 1 Then Throw New Exception("RequestTestDevice WrongAnswerData")
            If result.Data(3) <> req.Data(3) + 1 Then Throw New Exception("RequestTestDevice WrongAnswerData")
        Else
            Throw New Exception("RequestTestDevice " + result.ResponseState.ToString)
        End If
    End Sub

    Public Function RequestDeviceInfo(address As Integer) As DeviceInfo
        Dim result = Request(address, 120, {}, 5)
        If result.ResponseState = ResponseState.ok Then
            Dim info As New DeviceInfo
            If result.Response = 121 Then
                info.Address = result.Address
                info.Family = result.Data(0) * 256 + result.Data(1)
                info.Version = result.Data(3)
                info.Model = result.Data(2)
                Return info
            Else
                Throw New Exception("RequestDeviceInfo WrongResponse  " + result.ResponseState.ToString)
            End If
        Else
            Throw New Exception("RequestDeviceInfo " + result.ResponseState.ToString)
        End If
    End Function

End Class


