

Public Class CatUartManager
    Public Class RequestListItem
        Public Property Request As Request
        Public Property ResponseHandler As ResponseHandlerDelegate
    End Class


    Private _logger As Logger
    Private _cus As New List(Of CatUart)
    Private _storage As SettingsStorage
    Private _portNames As StringSetting
    Private _dic As New Dictionary(Of Integer, Integer)
    Private _lastPorts As String = ""
    Private _requestList As New List(Of RequestListItem)

    Private _sensorLines As New KeyValue(Of Integer, Integer)(0)
    Public Delegate Sub ResponseHandlerDelegate(source As CatUartManager, request As Request, response As Response)
    Public Event RequestListIsEmpty(source As CatUartManager)

    Public Sub New(logger As Logger, storage As SettingsStorage)
        _logger = logger
        _storage = storage
        _portNames = New StringSetting(_storage, "SerialPort", "COM1", "Последовательные порты, через запятую", "")
        Dim worker = New Threading.Thread(AddressOf WorkerThreadSub)
        worker.Priority = Threading.ThreadPriority.BelowNormal
        worker.IsBackground = True
        worker.Name = "CatUartManager Worker"
        worker.Start()
    End Sub

    Public ReadOnly Property PortNames As StringSetting
        Get
            Return _portNames
        End Get
    End Property

    Public ReadOnly Property RequestList As RequestListItem()
        Get
            SyncLock _requestList
                Return _requestList.ToArray
            End SyncLock
        End Get
    End Property

    Public Function CheckConnection()
        If _lastPorts <> _portNames.Value Then
            For Each cu In _cus
                Try : cu.SerialDevice.Disconnect() : Catch ex As Exception : End Try
            Next
            _cus.Clear()
            Dim portNames = _portNames.Value.Split(","c)
            For Each portName In portNames
                portName = portName.Trim
                If portName > "" Then
                    Dim md As New CatUart(New FastSerialPort())
                    md.SerialDevice.DeviceAddress = portName
                    _cus.Add(md)
                End If
            Next
            _lastPorts = _portNames.Value
            _logger.AddMessage("Создано интерфейсов: " + _cus.Count.ToString)
        End If

        Dim i, connected As Integer
        For Each md In _cus
            i += 1
            If Not md.SerialDevice.IsConnected Then
                _logger.AddMessage("Линия #" + i.ToString + " не подключена! Попытка подключить " + md.SerialDevice.DeviceAddress)
                Threading.Thread.Sleep(100)
                Try
                    md.SerialDevice.Connect()
                Catch ex As Exception
                    _logger.AddWarning("Линия #" + i.ToString + " - " + md.SerialDevice.DeviceAddress + " - " + ex.Message)
                End Try
            End If
            If md.SerialDevice.IsConnected Then connected += 1
        Next

        Return connected > 0
    End Function

    Public Sub AddRequest(request As Request, responseHandler As ResponseHandlerDelegate)
        SyncLock _requestList
            _requestList.Add(New RequestListItem With {.Request = request, .ResponseHandler = responseHandler})
        End SyncLock
    End Sub

    Public Sub WorkerThreadSub()
        Do
            Dim task As RequestListItem = Nothing
            SyncLock _requestList
                If _requestList.Count > 0 Then task = _requestList(0)
            End SyncLock
            If task IsNot Nothing Then
                Try
                    Dim response As New Response With {.ResponseState = ResponseState.errorNotRequested}
                    ' If task.Request.Command = 30 Then Stop
                    Debug.Write(Now.Millisecond.ToString + ", ")

                    If CheckConnection() Then response = GetResponse(task.Request)
                    Debug.WriteLine(Now.Millisecond.ToString + ", " + task.Request.Command.ToString + ", " + response.ResponseState.ToString)
                    task.ResponseHandler.Invoke(Me, task.Request, response)
                Catch ex As Exception
                End Try
                SyncLock _requestList
                    Try
                        _requestList.Remove(task)
                    Catch ex As Exception : End Try
                End SyncLock
            Else
                RaiseEvent RequestListIsEmpty(Me)
            End If
            Threading.Thread.Sleep(10)
        Loop
    End Sub

    Private Function GetResponse(request As Request) As Response

        Dim response As New Response With {.ResponseState = ResponseState.errorNotRequested}
        Dim preferredLine = _sensorLines(request.Address)

        Try
            response = _cus(preferredLine).Request(request)
            If response.ResponseState = ResponseState.ok Then
                Return response
            End If
        Catch ex As Exception
        End Try

        For line = 0 To _cus.Count - 1
            If line <> preferredLine Then
                Try
                    response = _cus(line).Request(request)
                    If response.ResponseState = ResponseState.ok Then
                        _sensorLines(request.Address) = line
                        Return response
                    End If
                Catch ex As Exception
                End Try
            End If
        Next

        Return response
    End Function

End Class
Public Class KeyValue(Of TKey, TValue As New)
    Inherits Dictionary(Of TKey, TValue)
    Private _defaultValue As TValue
    Public Sub New(defaultValue As TValue)
        _defaultValue = defaultValue
    End Sub
    Default Public Shadows Property Item(key As TKey) As TValue
        Get
            Try
                Dim value = MyBase.Item(key)
                Return value
            Catch ex As Exception
                If _defaultValue Is Nothing Then
                    MyBase.Add(key, New TValue)
                Else
                    MyBase.Add(key, _defaultValue)
                End If
                Dim value = MyBase.Item(key)
                Return value
            End Try
        End Get
        Set(value As TValue)
            Try
                MyBase.Item(key) = value
            Catch ex As Exception
                MyBase.Add(key, value)
            End Try
        End Set
    End Property
End Class
