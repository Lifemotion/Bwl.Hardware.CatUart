Imports Bwl.Hardware.Serial
''' <summary>
''' Реализация базового интерфейса шины CatUart.
''' </summary>
''' <remarks></remarks>
Public Class CatUart
    Private _serial As ISerialDevice
    Private _lastPacket As Request
    Private _syncRoot As New Object

    Public Sub New(serial As ISerialDevice)
        _serial = serial
        _serial.DeviceSpeed=9600
        _serial.AutoReadBytes = False
    End Sub


    ' 9600 - стандарт, 1200 - low, 76800 - fast

    ''' <summary>
    ''' Устройство последовательной связи.
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property SerialDevice As ISerialDevice
        Get
            Return _serial
        End Get
    End Property

    ''' <summary>
    ''' Обрезет значение в диапазон 0..127
    ''' </summary>
    ''' <param name="data"></param>
    ''' <remarks></remarks>
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

    ''' <summary>
    ''' Отправка пакета типа 8 - 4 байта данных, 1xCRC8
    ''' </summary>
    ''' <param name="address"></param>
    ''' <param name="command"></param>
    ''' <param name="data1"></param>
    ''' <remarks></remarks>
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

    ''' <summary>
    ''' Отправка пакета типа 5 - 0 байт данных, 1xCRC8
    ''' </summary>
    ''' <param name="address"></param>
    ''' <param name="command"></param>
    ''' <param name="data1"></param>
    ''' <remarks></remarks>
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

    ''' <summary>
    ''' Выполнить запрос и получить ответ. Тип пакета запроса - 8.
    ''' </summary>
    ''' <param name="requestPacket">Пакет данных запроса</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function Request(requestPacket As Request) As Response
        Return Request(requestPacket.Address, requestPacket.Command, requestPacket.Data)
    End Function
    ''' <summary>
    ''' Выполнить запрос и получить ответ. Тип пакета запроса - 8.
    ''' </summary>
    ''' <param name="address">Адрес отвечающего устройства.</param>
    ''' <param name="command">Команда устройству.</param>
    ''' <param name="dataRequest">Массив данных для передачи устройству.</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function Request(address As Integer, command As Integer, dataRequest As Integer()) As Response
        Return Request(address, command, dataRequest, 8)
    End Function

    Private _readBufferPos As Integer
    Private _readBuffer(32) As Byte

    Public Function Read() As Response
        SyncLock _syncRoot
            Dim result As New Response
            result.ResponseState = ResponseState.errorNotRequested

            SyncLock _serial
                Do While _serial.ReceivedBufferCount > 0 And result.ResponseState <> ResponseState.ok
                    Dim data = _serial.Read()
                    ReadBytes += 1
                    Select Case data
                        Case &HFD
                            _readBufferPos = 0
                        Case &HFB
                            Debug.WriteLine(_readBufferPos.ToString)
                            result.Address = _readBuffer(0) * 128 * 128 + _readBuffer(1) * 128 + _readBuffer(2)
                            result.Type = _readBuffer(3)
                            If result.Type = 8 Then
                                If _readBufferPos = 10 Then
                                    result.Response = _readBuffer(4)
                                    result.Data(0) = _readBuffer(5)
                                    result.Data(1) = _readBuffer(6)
                                    result.Data(2) = _readBuffer(7)
                                    result.Data(3) = _readBuffer(8)
                                    Dim crc1 = _readBuffer(9)
                                    Dim crc1real = Crc8.ComputeCrc(&HAA, _readBuffer, 0, 8) Mod 128
                                    If crc1 = crc1real Then
                                        result.ResponseState = ResponseState.ok
                                    Else
                                        result.ResponseState = ResponseState.errorCrc
                                    End If
                                Else
                                    result.ResponseState = ResponseState.errorFormat
                                End If
                            ElseIf result.Type = 10 Then
                                If _readBufferPos = 14 Then
                                    result.Response = _readBuffer(4)
                                    result.Data(0) = _readBuffer(5)
                                    result.Data(1) = _readBuffer(6)
                                    result.Data(2) = _readBuffer(7)
                                    result.Data(3) = _readBuffer(8)
                                    result.Data(4) = _readBuffer(9)
                                    result.Data(5) = _readBuffer(10)
                                    result.Data(6) = _readBuffer(11)
                                    result.Data(7) = _readBuffer(12)
                                    Dim crc1 = _readBuffer(13)
                                    Dim crc1real = Crc8.ComputeCrc(&HAA, _readBuffer, 0, 12) And 127
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
                            _readBufferPos = 0
                        Case Else
                            If _readBufferPos < _readBuffer.Length - 1 Then
                                _readBuffer(_readBufferPos) = data
                                _readBufferPos += 1
                            End If
                    End Select
                Loop
            End SyncLock
            If result.ResponseState = ResponseState.ok Then
                Return result
            Else
                Return Nothing
            End If
        End SyncLock
    End Function

    Public Property ReadBytes As Long

    ''' <summary>
    ''' Выполнить запрос и получить ответ. Тип пакета запроса может быть указан..
    ''' </summary>
    ''' <param name="address">Адрес отвечающего устройства.</param>
    ''' <param name="command">Команда устройству.</param>
    ''' <param name="dataRequest">Массив данных для передачи устройству.</param>
    ''' <param name="type">Тип пакета данных.</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function Request(address As Integer, command As Integer, dataRequest As Integer(), type As Integer) As Response
        SyncLock _syncRoot
            Dim result As New Response
            SyncLock _serial
                Do While _serial.ReceivedBufferCount > 0
                    _serial.Read()
                Loop
                Select Case type
                    Case 8
                        SendPacket08(address, command, dataRequest)
                    Case 5
                        SendPacket05(address, command, dataRequest)
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
                ' If result.ResponseState = ResponseState.errorTimeout Then Stop
                Return result
            End SyncLock
        End SyncLock
    End Function

    Public Function QuickRequest(address As Integer) As Integer
        Throw New Exception("Not implemented")
    End Function

    ''' <summary>
    ''' Запросить тестирование устройства (команда 122).
    ''' </summary>
    ''' <param name="address">Адрес устройства.</param>
    ''' <remarks></remarks>
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

    ''' <summary>
    ''' Запросить базовую информацию устройства (команда 120).
    ''' </summary>
    ''' <param name="address">Адрес устройства.</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function RequestDeviceInfo(address As Integer) As DeviceInfo
        Dim result = Request(address, 120, {}, 5)
        Dim info As DeviceInfo = result

        If result.ResponseState = ResponseState.ok Then
            If result.Response = 121 Then
                info.Address = result.Address
                info.DeviceFamily = result.Data(0) * 256 + result.Data(1)
                info.DeviceVersion = result.Data(3)
                info.DeviceModel = result.Data(2)
                Return info
            Else
                '  Throw New Exception("RequestDeviceInfo WrongResponse  " + result.ResponseState.ToString)
            End If
        Else
            ' Throw New Exception("RequestDeviceInfo " + result.ResponseState.ToString)
        End If

        Return result
    End Function

End Class


