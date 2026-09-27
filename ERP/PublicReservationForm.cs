using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    public class PublicReservationForm : Form
    {
        private readonly HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5166/") };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly string _companyCode;

        private TextBox txtName = null!, txtContact = null!, txtEmail = null!;
        private ComboBox cboRoom = null!;
        private DateTimePicker dtpCheckIn = null!, dtpCheckOut = null!;
        private NumericUpDown numGuests = null!;
        private Label lblStatus = null!;

        public PublicReservationForm(string companyCode)
        {
            _companyCode = companyCode;
            BuildUi();
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-Company-Code", _companyCode);
            return request;
        }

        private void BuildUi()
        {
            this.Text = "Request a Reservation";
            this.Width = 480;
            this.Height = 560;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.BackColor = AppTheme.Background;

            var lblTitle = new Label { Text = "New Reservation Request", Font = AppTheme.SubHeadingFont, ForeColor = AppTheme.TextDark, Left = 30, Top = 20, Width = 400 };

            var lblName = new Label { Text = "Full Name", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 65, Width = 200 };
            txtName = new TextBox { Left = 30, Top = 88, Width = 400, Font = AppTheme.BodyFont };

            var lblContact = new Label { Text = "Contact Number", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 125, Width = 200 };
            txtContact = new TextBox { Left = 30, Top = 148, Width = 400, Font = AppTheme.BodyFont };

            var lblEmail = new Label { Text = "Email", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 185, Width = 200 };
            txtEmail = new TextBox { Left = 30, Top = 208, Width = 400, Font = AppTheme.BodyFont };

            var lblRoom = new Label { Text = "Room", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 245, Width = 200 };
            cboRoom = new ComboBox { Left = 30, Top = 268, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList, Font = AppTheme.BodyFont };

            var lblCheckIn = new Label { Text = "Check-In", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 305, Width = 190 };
            dtpCheckIn = new DateTimePicker { Left = 30, Top = 328, Width = 190, Font = AppTheme.BodyFont };

            var lblCheckOut = new Label { Text = "Check-Out", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 240, Top = 305, Width = 190 };
            dtpCheckOut = new DateTimePicker { Left = 240, Top = 328, Width = 190, Font = AppTheme.BodyFont, Value = DateTime.Now.AddDays(1) };

            var lblGuests = new Label { Text = "Guests", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 365, Width = 190 };
            numGuests = new NumericUpDown { Left = 30, Top = 388, Width = 100, Minimum = 1, Maximum = 20, Value = 1, Font = AppTheme.BodyFont };

            var btnLoadRooms = AppTheme.CreateIconButton("\u21bb", "Refresh Rooms", 240, 388, 190, 32, AppTheme.LightTeal, AppTheme.DarkTeal);
            btnLoadRooms.Click += async (s, e) => await LoadRoomsAsync();

            var btnSubmit = AppTheme.CreateIconButton("\u2713", "Submit Request", 30, 435, 400, 50, AppTheme.Teal, System.Drawing.Color.White);
            btnSubmit.Click += async (s, e) => await SubmitAsync();

            lblStatus = new Label { Left = 30, Top = 495, Width = 400, Height = 40, Font = new System.Drawing.Font("Segoe UI", 8.5F), ForeColor = AppTheme.TextDark, Text = "" };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblContact);
            this.Controls.Add(txtContact);
            this.Controls.Add(lblEmail);
            this.Controls.Add(txtEmail);
            this.Controls.Add(lblRoom);
            this.Controls.Add(cboRoom);
            this.Controls.Add(lblCheckIn);
            this.Controls.Add(dtpCheckIn);
            this.Controls.Add(lblCheckOut);
            this.Controls.Add(dtpCheckOut);
            this.Controls.Add(lblGuests);
            this.Controls.Add(numGuests);
            this.Controls.Add(btnLoadRooms);
            this.Controls.Add(btnSubmit);
            this.Controls.Add(lblStatus);

            this.Load += async (s, e) => await LoadRoomsAsync();
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                var request = NewRequest(HttpMethod.Get, "rooms");
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var rooms = JsonSerializer.Deserialize<List<RoomDto>>(json, JsonOptions) ?? new();

                cboRoom.Items.Clear();
                foreach (var r in rooms)
                {
                    if (r.Status == "Available")
                        cboRoom.Items.Add(new LookupItem { Id = r.RoomId, Display = $"{r.RoomNumber} - {r.RoomType} (\u20b1{r.Rate:N2}/night)" });
                }

                lblStatus.Text = cboRoom.Items.Count == 0 ? "No available rooms found right now." : "";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Could not load rooms: " + ex.Message;
            }
        }

        private async Task SubmitAsync()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtContact.Text))
            {
                lblStatus.Text = "Full Name and Contact Number are required.";
                return;
            }
            if (cboRoom.SelectedItem is not LookupItem room)
            {
                lblStatus.Text = "Please select a room.";
                return;
            }
            if (dtpCheckOut.Value <= dtpCheckIn.Value)
            {
                lblStatus.Text = "Check-Out must be after Check-In.";
                return;
            }

            try
            {
                var customerCode = "WALKIN-" + DateTime.UtcNow.Ticks;
                var newCustomer = new
                {
                    customerCode,
                    customerName = txtName.Text.Trim(),
                    contactNumber = txtContact.Text.Trim(),
                    emailAddress = txtEmail.Text.Trim(),
                    address = "",
                    isActive = true
                };

                var custRequest = NewRequest(HttpMethod.Post, "customers");
                custRequest.Content = new StringContent(JsonSerializer.Serialize(newCustomer), Encoding.UTF8, "application/json");
                var custResponse = await _http.SendAsync(custRequest);
                custResponse.EnsureSuccessStatusCode();
                var custJson = await custResponse.Content.ReadAsStringAsync();
                var createdCustomer = JsonSerializer.Deserialize<CustomerLookupDto>(custJson, JsonOptions);

                var newReservation = new
                {
                    customerId = createdCustomer!.CustomerId,
                    roomId = room.Id,
                    reservationDate = DateTime.UtcNow,
                    checkIn = dtpCheckIn.Value,
                    checkOut = dtpCheckOut.Value,
                    numberOfGuests = (int)numGuests.Value,
                    status = "Pending",
                    remarks = "Submitted via public self-service form"
                };

                var resRequest = NewRequest(HttpMethod.Post, "reservations");
                resRequest.Content = new StringContent(JsonSerializer.Serialize(newReservation), Encoding.UTF8, "application/json");
                var resResponse = await _http.SendAsync(resRequest);
                resResponse.EnsureSuccessStatusCode();

                MessageBox.Show("Your reservation request has been submitted! Our staff will confirm it shortly.", "Request Sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Failed to submit: " + ex.Message;
            }
        }
    }
}