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
    Protected Function FitToByte1(data As Integer) As Byte
        If data < 0 Then data = 0
        If data > 255 Then data = 255
        Dim tmp As Byte = data
        tmp = tmp And 127
        data = tmp
        Return data
    End Function

    Public Sub SendPacket(packet As Request)
        SendPacket(packet.Address, packet.Command, packet.Data, packet.PreferredType)
    End Sub

    Public Sub SendPacket(address As Integer, command As Integer, data1 As Integer(), type As Integer)
        If type = 0 Then SendPacket01(address, command, data1)
        If type = 1 Then SendPacket01(address, command, data1)
        If type = 5 Then SendPacket05(address, command, data1)
        If type = 8 Then SendPacket08(address, command, data1)
        If type = 10 Then SendPacket10(address, command, data1)
    End Sub

    Protected Sub SendPacketUniversalCrc8(typecode As Byte, address As Integer, command As Integer, data As Integer(), datalength As Integer, fixeddatalenth As Boolean, crcstart As Byte)
        If datalength > 127 Then datalength = 127
        Dim bytes(1024) As Byte
        bytes(0) = 0
        bytes(1) = &HFE
        bytes(2) = address \ 128 \ 128
        bytes(3) = (address \ 128) Mod 128
        bytes(4) = address Mod 128
        bytes(5) = typecode
        bytes(6) = FitToByte1(command)
        Dim offset = 7
        If Not fixeddatalenth Then bytes(7) = datalength : offset += 1
        For i = 0 To datalength - 1
            bytes(offset + i) = FitToByte1(data(i))
        Next
        bytes(offset + datalength) = FitToByte1(Crc8.ComputeCrc(crcstart, bytes, 2, offset + datalength - 1))
        bytes(offset + datalength + 1) = &HFC
        ReDim Preserve bytes(offset + datalength + 1)
        _serial.Write(bytes)
    End Sub

    ''' <summary>
    ''' Отправка пакета типа 1 - 0-127 байт данных, 1xCRC8
    ''' </summary>
    ''' <param name="address"></param>
    ''' <param name="command"></param>
    ''' <param name="data"></param>
    ''' <remarks></remarks>
    Protected Sub SendPacket01(address As Integer, command As Integer, data As Integer())
        SendPacketUniversalCrc8(1, address, command, data, data.Length, False, 170)
    End Sub

    ''' <summary>
    ''' Отправка пакета типа 8 - 4 байта данных, 1xCRC8
    ''' </summary>
    ''' <param name="address"></param>
    ''' <param name="command"></param>
    ''' <param name="data"></param>
    ''' <remarks></remarks>
    Protected Sub SendPacket08(address As Integer, command As Integer, data As Integer())
        SendPacketUniversalCrc8(8, address, command, data, 4, True, 170)
    End Sub

    ''' <summary>
    ''' Отправка пакета типа 10 - 8 байт данных, 1xCRC8
    ''' </summary>
    ''' <param name="address"></param>
    ''' <param name="command"></param>
    ''' <param name="data"></param>
    ''' <remarks></remarks>
    Protected Sub SendPacket10(address As Integer, command As Integer, data As Integer())
        SendPacketUniversalCrc8(&H10, address, command, data, 8, True, 170)
    End Sub

    ''' <summary>
    ''' Отправка пакета типа 5 - нет байт данных, 1xCRC8
    ''' </summary>
    ''' <param name="address"></param>
    ''' <param name="command"></param>
    ''' <param name="data"></param>
    ''' <remarks></remarks>
    Protected Sub SendPacket05(address As Integer, command As Integer, data As Integer())
        SendPacketUniversalCrc8(&H5, address, command, data, 0, True, 170)
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
        Return Request(address, command, dataRequest, 1)
    End Function

    Private _readBufferPos As Integer
    Private _readBuffer(1024) As Byte

    Public Function Read() As Response
        SyncLock _syncRoot
            Dim result As New Response
            result.ResponseState = ResponseState.errorNotRequested
            SyncLock _serial
                Dim readSuccess As Boolean = True
                Dim data As Byte
                Do While result.ResponseState <> ResponseState.ok And readSuccess
                    readSuccess = False
                    Try
                        If _serial.ReceivedBufferCount > 0 Then
                            data = _serial.Read()
                            readSuccess = True
                        End If
                    Catch ex As Exception
                        result.ResponseState = ResponseState.errorPortError
                        Return result
                    End Try
                    If readSuccess Then
                        ReadBytes += 1
                        Select Case data
                            Case &HFD
                                _readBufferPos = 0
                            Case &HFB
                                Debug.WriteLine(_readBufferPos.ToString)
                                result.Address = _readBuffer(0) * 128 * 128 + _readBuffer(1) * 128 + _readBuffer(2)
                                result.Type = _readBuffer(3)
                                Select Case result.Type
                                    Case 5 : ProcessPacketCrc8(result, 0, True, _readBufferPos, _readBuffer)
                                    Case 8 : ProcessPacketCrc8(result, 4, True, _readBufferPos, _readBuffer)
                                    Case 10, &H10 : ProcessPacketCrc8(result, 8, True, _readBufferPos, _readBuffer)
                                    Case 1 : ProcessPacketCrc8(result, 0, False, _readBufferPos, _readBuffer)
                                    Case Else
                                        result.ResponseState = ResponseState.errorPacketType
                                End Select
                                _readBufferPos = 0

                            Case Else
                                If _readBufferPos < _readBuffer.Length - 1 Then
                                    _readBuffer(_readBufferPos) = data
                                    _readBufferPos += 1
                                End If
                        End Select
                    End If
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


    Public Sub ProcessPacketCrc8(result As Response, datalength As Integer, fixeddatalength As Boolean, ByRef receivedLength As Integer, receivedBuffer() As Byte)

        result.Response = receivedBuffer(4)
        result.DataLength = 0
        Dim offset = 5
        If Not fixeddatalength Then datalength = receivedBuffer(5) : offset += 1

        If receivedLength = offset + datalength + 1 Then
            For i = 0 To datalength - 1
                result.Data(i) = receivedBuffer(i + offset)
            Next
            Dim crc1 = receivedBuffer(offset + datalength)
            Dim crc1real = Crc8.ComputeCrc(&HAA, receivedBuffer, 0, offset + datalength - 1) Mod 128
            If crc1 = crc1real Then
                result.ResponseState = ResponseState.ok
                result.DataLength = datalength
            Else
                result.ResponseState = ResponseState.errorCrc
            End If
        Else
            result.ResponseState = ResponseState.errorFormat
        End If
    End Sub

    Public Function RequestWithRetries(address As Integer, command As Integer, dataRequest As Integer(), retries As Integer) As Response
        For i = 1 To retries - 1
            Dim result = Request(address, command, dataRequest)
            If result.ResponseState = ResponseState.ok Then Return result
            Debug.WriteLine("Retry " + command.ToString)
            Threading.Thread.Sleep(200)
        Next
        Return Request(address, command, dataRequest)
    End Function

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
                Try
                    Do While _serial.ReceivedBufferCount > 0
                        _serial.Read()
                    Loop
                Catch ex As Exception
                End Try
                SendPacket(address, command, dataRequest, type)
                result.ResponseState = ResponseState.errorTimeout
                Dim time = Now
                Dim receivedLength As Integer
                Dim receivedBuffer(1024) As Byte


                Do While (Now - time).TotalMilliseconds < RequestTimeout And result.ResponseState = ResponseState.errorTimeout

                    Dim readSuccess As Boolean = False
                    Dim data As Byte

                    Try
                        If _serial.ReceivedBufferCount > 0 Then
                            data = _serial.Read()
                            readSuccess = True
                        End If
                    Catch ex As Exception
                        result.ResponseState = ResponseState.errorPortError
                        Return result
                    End Try

                    If readSuccess Then
                        Select Case data
                            Case &HFD
                                receivedLength = 0
                            Case &HFB
                                result.Address = receivedBuffer(0) * 128 * 128 + receivedBuffer(1) * 128 + receivedBuffer(2)
                                result.Type = receivedBuffer(3)
                                Select Case result.Type
                                    Case 5 : ProcessPacketCrc8(result, 0, True, receivedLength, receivedBuffer)
                                    Case 8 : ProcessPacketCrc8(result, 4, True, receivedLength, receivedBuffer)
                                    Case 10, &H10 : ProcessPacketCrc8(result, 8, True, receivedLength, receivedBuffer)
                                    Case 1 : ProcessPacketCrc8(result, 0, False, receivedLength, receivedBuffer)
                                    Case Else
                                        result.ResponseState = ResponseState.errorPacketType
                                End Select
                            Case Else
                                If receivedLength < receivedBuffer.Length - 1 Then
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
        Dim info As New DeviceInfo With {.Response = result}

        If result.ResponseState = ResponseState.ok Then
            If result.Response = 121 Then
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

        Return info
    End Function

End Class


