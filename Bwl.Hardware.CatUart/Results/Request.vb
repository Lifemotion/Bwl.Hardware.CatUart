Public Class Request
    Sub New()

    End Sub
    Sub New(command As Integer, address As Integer)
        _Command = command
        _Address = address
    End Sub
    Public Property Command As Integer
    Public Property Address As Integer
    Public Property Data As Integer() = Array.CreateInstance(GetType(Integer), 16)
End Class
