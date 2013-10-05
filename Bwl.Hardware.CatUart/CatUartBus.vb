Public Class CatUartBus
    Private _catUart As CatUart
    Public ReadOnly Property CatUart As CatUart
        Get
            Return _catUart
        End Get
    End Property
    Sub New(catUart As CatUart)
        _catUart = catUart
    End Sub

End Class
