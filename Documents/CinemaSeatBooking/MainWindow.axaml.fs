namespace CinemaSeatBooking

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Interactivity

type MainWindow () as this = 
    inherit Window ()

    let mutable rowsInput : TextBox = null
    let mutable colsInput : TextBox = null
    let mutable createButton : Button = null

    do 
        this.InitializeComponent()
        rowsInput <- this.FindControl<TextBox>("RowsTextBox")
        colsInput <- this.FindControl<TextBox>("ColsTextBox")
        createButton <- this.FindControl<Button>("CreateButton")
        createButton.Click.Add(fun _ -> this.OnCreateClicked())

    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)

    member private this.OnCreateClicked() =
        let successRows, rows = Int32.TryParse(rowsInput.Text)
        let successCols, cols = Int32.TryParse(colsInput.Text)

        if successRows && successCols then
            
            let seatMapWindow = SeatMapWindow(rows, cols)
            seatMapWindow.Show()
        
