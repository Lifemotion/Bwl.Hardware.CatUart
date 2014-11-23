﻿Public Class Response
    Sub New()

    End Sub

    Public Property Response As Integer
    Public Property Type As Integer
    Public Property ResponseState As ResponseState
    Public Property Address As Integer
    Public Property Data As Integer() = Array.CreateInstance(GetType(Integer), 1024)
    Public Property DataLength As Integer
End Class

Public Enum ResponseState
    ok
    errorTimeout
    errorFormat
    errorCrc
    errorPacketType
    errorNotRequested

    errorPortError

End Enum
