namespace CinemaSeatBooking

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open CinemaSeatBooking.SeatManagement
open CinemaSeatBooking.BookingLogic




type SeatMapWindow(rows: int, cols: int) as this =
    inherit Window()

    // ===== عناصر الواجهة الرسومية =====
    let mutable seatsPanel : StackPanel = null          // لعرض خريطة الكراسي
    let mutable numSeatsTextBox : TextBox = null       // مدخل عدد الكراسي اللي عايزة احجزهم
    let mutable generateButton : Button = null         // زرار توليد المدخلات
    let mutable seatsInputPanel : StackPanel = null    // لوحة مدخلات الصفوف والأعمدة
    let mutable reserveButton : Button = null          // زرار حجز الكراسي
    let mutable ticketsButton : Button = null          // زرار عرض التيكتات
    let mutable statusMessage : TextBlock = null       // (رسالة  (نجاح/خطأ)

    // ===== الحالة الداخلية للسينما =====
    let mutable cinemaState: CinemaState = 
        { Seats = SeatManagement.initializeSeatLayout rows cols; Tickets = [] } // initializeSeatLayout بترجع كل الكراسي كـ Available

    do
        // ===== تحميل XAML وربط عناصر الواجهة =====
        this.InitializeComponent()
        seatsPanel <- this.FindControl<StackPanel>("SeatsPanel")
        numSeatsTextBox <- this.FindControl<TextBox>("NumSeatsTextBox")
        generateButton <- this.FindControl<Button>("GenerateInputsButton")
        seatsInputPanel <- this.FindControl<StackPanel>("SeatsInputPanel")
        reserveButton <- this.FindControl<Button>("ReserveButton")
        ticketsButton <- this.FindControl<Button>("TicketsButton")
        statusMessage <- this.FindControl<TextBlock>("StatusMessageTextBlock")

        ticketsButton.IsVisible <- false // في البداية زرار التيكت مخفي
        seatsPanel.HorizontalAlignment <- HorizontalAlignment.Left

        // ===== عرض الكراسي عند فتح البرنامج =====
        this.GenerateSeats()

        // ===== ربط الأحداث بالزرار =====
        generateButton.Click.Add(fun _ -> this.GenerateSeatInputs() |> ignore)
        reserveButton.Click.Add(fun _ -> this.ReserveSeatsFromInputs() |> ignore)
        ticketsButton.Click.Add(fun _ -> this.ShowTicketsPage() |> ignore)

    // ===== تحميل XAML =====
    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    // ===== إنشاء خلية كرسي واحدة مع اللون والرمز =====
    member private this.CreateSeatCell(seat: Seat) =
        let border = new Border(Width=60.0, Height=60.0, CornerRadius=CornerRadius(5.0), Margin=Thickness(5.0))
        let text = new TextBlock(HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, FontSize=20.0)
        match seat.Status with
        | SeatStatus.Available -> 
            border.Background <- SolidColorBrush(Color.Parse("#FFEB99"))
            text.Foreground <- Brushes.White
            text.Text <- "O"   // كرسي متاح
        | SeatStatus.Reserved -> 
            border.Background <- Brushes.Red
            text.Foreground <- Brushes.White
            text.Text <- "X"   // كرسي محجوز
        border.Child <- text
        border

    // ===== عرض كل الكراسي + الإحصائيات =====
    member private this.GenerateSeats() =
        seatsPanel.Children.Clear()
        let availableCount = getAvailableSeats cinemaState.Seats |> List.length
        let reservedCount = getReservedSeats cinemaState.Seats |> List.length

        // ===== عرض الإحصائيات =====
        let stats = new TextBlock(Text=sprintf "Available: %d | Reserved: %d" availableCount reservedCount,
                                  FontSize=16.0, Foreground=Brushes.Black, Margin=Thickness(0.0,0.0,0.0,10.0))
        seatsPanel.Children.Add(stats) |> ignore

        // ===== عرض أسماء الأعمدة =====
        let headerRow = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Left)
        headerRow.Children.Add(new TextBlock(Text=" ", Width=40.0)) |> ignore
        for c in 1..cols do
            let colLabel = new TextBlock(Text=sprintf "%d" c, Width=60.0, FontSize=16.0,
                                         HorizontalAlignment=HorizontalAlignment.Center, TextAlignment=TextAlignment.Center)
            headerRow.Children.Add(colLabel) |> ignore
        seatsPanel.Children.Add(headerRow) |> ignore

        // ===== عرض كل الصفوف والكراسي =====
        for r in 1..rows do
            let rowPanel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Left)
            let rowLabel = new TextBlock(Text=sprintf "%d" r, Width=40.0, FontSize=16.0,
                                         HorizontalAlignment=HorizontalAlignment.Center, TextAlignment=TextAlignment.Center)
            rowPanel.Children.Add(rowLabel) |> ignore
            for c in 1..cols do
                match selectSeat r c cinemaState.Seats with
                | Some seat -> rowPanel.Children.Add(this.CreateSeatCell(seat)) |> ignore
                | None -> ()
            seatsPanel.Children.Add(rowPanel) |> ignore

    // ===== توليد مدخلات الحجز (TextBox لكل كرسي) =====
    member private this.GenerateSeatInputs() =
        seatsInputPanel.Children.Clear()
        statusMessage.Text <- ""
        let ok, count = Int32.TryParse(numSeatsTextBox.Text)
        if ok && count > 0 then
            for i in 1..count do
                let rowBox = new TextBox(Watermark=sprintf "Row %d" i, Width=60.0)
                let colBox = new TextBox(Watermark=sprintf "Col %d" i, Width=60.0)
                let panel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0)
                panel.Children.Add(rowBox) |> ignore
                panel.Children.Add(colBox) |> ignore
                seatsInputPanel.Children.Add(panel) |> ignore
        else
            statusMessage.Text <- "Enter a valid number!"

    // ===== حجز الكراسي من المدخلات =====
    member private this.ReserveSeatsFromInputs() =
        statusMessage.Text <- ""
        try
            // ===== قراءة الصفوف والأعمدة من TextBox =====
            let positions =
                seatsInputPanel.Children
                |> Seq.cast<StackPanel>
                |> Seq.choose (fun panel ->
                    try
                        let rowBox = panel.Children.[0] :?> TextBox
                        let colBox = panel.Children.[1] :?> TextBox
                        let rOk, r = Int32.TryParse(rowBox.Text)
                        let cOk, c = Int32.TryParse(colBox.Text)
                        if rOk && cOk then Some(r, c, rowBox, colBox)
                        else
                            // ===== تلوين المدخلات باللون الأحمر لو غير صالحة =====
                            rowBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                            colBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                            None
                    with _ -> None)
                |> Seq.toList

            if positions.IsEmpty then
                statusMessage.Text <- "Enter valid row and column numbers!"
            else
                let seatPositions = positions |> List.map (fun (r,c,_,_) -> (r,c))

                // ===== منع الحجز المزدوج =====
                let allAvailable =
                    seatPositions
                    |> List.forall (fun (r,c) ->
                        match selectSeat r c cinemaState.Seats with
                        | Some seat -> preventDoubleBooking seat
                        | None -> false)

                if not allAvailable then
                    statusMessage.Text <- "Some seats are already reserved!"
                else
                    // ===== إنشاء التيكت =====
                    match createTicket seatPositions cinemaState.Seats with
                    | Error badSeats ->
                        // ===== تلوين المدخلات الغير صالحة =====
                        positions |> List.iter (fun (r,c,rowBox,colBox) ->
                            if badSeats |> List.contains (r,c) then
                                rowBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                                colBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                        )
                        statusMessage.Text <- "Some seats are invalid or already reserved!"
                    | Ok (ticket, updatedSeats) ->
                        // ===== تحديث الحالة الداخلية للسينما =====
                        cinemaState <- { cinemaState with Seats = updatedSeats; Tickets = ticket :: cinemaState.Tickets }
                        this.GenerateSeats()
                        statusMessage.Text <- "Reservation successful!"
                        ticketsButton.IsVisible <- true
        with ex ->
            statusMessage.Text <- sprintf "Error: %s" ex.Message

    // ===== صفحة عرض التيكتات مع أزرار Download & Back =====
    member private this.ShowTicketsPage() =
        let ticketsWindow = new Window(Title="Tickets", Width=900.0, Height=600.0)
        let imgBrush = new ImageBrush(Source=Bitmap("C:\\Users\\hp\\Documents\\Material\\PL3\\images\\Gemini_Generated_Image_weludaweludawelu.png"), Stretch=Stretch.UniformToFill)
        ticketsWindow.Background <- imgBrush

        let scroll = new ScrollViewer(Margin=Thickness(20.0))
        let panel = new StackPanel(Spacing=10.0)

        let title = new TextBlock(Text="Your Tickets", FontSize=28.0, Foreground=Brushes.Red,
                                  HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.0,0.0,0.0,20.0))
        panel.Children.Add(title) |> ignore

        // ===== أزرار Download و Back =====
        let buttonsPanel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Center)
        let downloadButton = new Button(Content="Download Tickets", Width=150.0, Background=SolidColorBrush(Color.Parse("#4CAF50")), Foreground=Brushes.White)
        let backButton = new Button(Content="Back", Width=100.0, Background=SolidColorBrush(Color.Parse("#FF5555")), Foreground=Brushes.White)
        buttonsPanel.Children.Add(downloadButton) |> ignore
        buttonsPanel.Children.Add(backButton) |> ignore
        panel.Children.Add(buttonsPanel) |> ignore

        // ===== تحميل تيكتات في ملف =====
        downloadButton.Click.Add(fun _ ->
            try
                let path = "Tickets.txt"
                use writer = new System.IO.StreamWriter(path)
                cinemaState.Tickets |> List.iter (fun ticket ->
                    writer.WriteLine(sprintf "Ticket ID: %A | Created At: %s" ticket.Id (ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")))
                    ticket.Seats |> List.iter (fun s ->
                        writer.WriteLine(sprintf "Seat: Row %d, Col %d" s.Row s.Col)
                    )
                    writer.WriteLine("--------------------------------------------------")
                )
                statusMessage.Text <- sprintf "Tickets saved to %s" path
            with ex ->
                statusMessage.Text <- sprintf "Error saving tickets: %s" ex.Message
        )

        // ===== زر العودة =====
        backButton.Click.Add(fun _ -> ticketsWindow.Close())

        // ===== عرض كل التيكتات =====
        if cinemaState.Tickets.Length > 0 then
            cinemaState.Tickets |> List.iter (fun ticket ->
                let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")), CornerRadius=CornerRadius(5.0),
                                        Padding=Thickness(10.0), Margin=Thickness(5.0))
                let stack = new StackPanel(Spacing=5.0)

                // ===== تفاصيل التيكت =====
                stack.Children.Add(
                    new TextBlock(
                        Text=sprintf "🎫 Ticket ID: %A | Created At: %s"
                            ticket.Id (ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                        FontSize=16.0, Foreground=Brushes.Red)) |> ignore

                ticket.Seats |> List.iter (fun s ->
                    stack.Children.Add(new TextBlock(Text=sprintf "Seat: Row %d, Col %d" s.Row s.Col,
                                                     FontSize=16.0, Foreground=Brushes.Red)) |> ignore)

                // ===== زر إلغاء التيكت =====
                let cancelButton = new Button(Content="Cancel Ticket", Width=120.0,
                                              Background=SolidColorBrush(Color.Parse("#FF5555")),
                                              Foreground=Brushes.White)

                cancelButton.Click.Add(fun _ ->
                    match cancelReservation ticket.Id cinemaState with
                    | Error msg -> statusMessage.Text <- msg
                    | Ok updatedState ->
                        cinemaState <- updatedState
                        this.GenerateSeats()
                        ticketsWindow.Content <- null
                        this.ShowTicketsPage()
                )

                stack.Children.Add(cancelButton) |> ignore
                border.Child <- stack
                panel.Children.Add(border) |> ignore)
        else
            // ===== حالة عدم وجود تيكتات =====
            let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")),
                                    CornerRadius=CornerRadius(5.0), Padding=Thickness(10.0),
                                    Margin=Thickness(5.0))
            let textBlock = new TextBlock(Text="No tickets booked yet!", FontSize=16.0, Foreground=Brushes.Red)
            border.Child <- textBlock
            panel.Children.Add(border) |> ignore

        scroll.Content <- panel
        ticketsWindow.Content <- scroll
        ticketsWindow.Show() |> ignore
