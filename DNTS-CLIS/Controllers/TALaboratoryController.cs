﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DNTS_CLIS.Controllers
{
    public class TALaboratoryController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public TALaboratoryController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DNTS_CLISContext");
        }

        public IActionResult Index()
        {
            string assignedLab = HttpContext.Session.GetString("AssignedLaboratory");
            if (string.IsNullOrWhiteSpace(assignedLab))
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.LaboratoryName = assignedLab;
            ViewBag.TrackNos = GetTrackNosForLaboratory(assignedLab);

            return View();
        }

        private List<string> GetTrackNosForLaboratory(string laboratoryName)
        {
            var trackNos = new List<string>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var query = "SELECT DISTINCT TrackNo FROM AssignedLaboratories WHERE LaboratoryName = @LaboratoryName";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@LaboratoryName", laboratoryName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trackNos.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return trackNos;
        }

        [HttpGet]
        public JsonResult GetTableDataOne(string trackNo)
        {
            if (string.IsNullOrWhiteSpace(trackNo))
            {
                return Json(new { error = "TrackNo is required." });
            }

            var dataTable = new DataTable();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var query = $"SELECT * FROM [{trackNo}]";

                using (var adapter = new SqlDataAdapter(query, conn))
                {
                    adapter.Fill(dataTable);
                }
            }
            var columnNames = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            var result = dataTable.AsEnumerable()
                .Select(row => {
                    var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    if (columnNames.Any(c => c.Equals("ID", StringComparison.OrdinalIgnoreCase)))
                        dict["Id"] = row[columnNames.First(c => c.Equals("ID", StringComparison.OrdinalIgnoreCase))].ToString();

                    if (columnNames.Any(c => c.Equals("CTN", StringComparison.OrdinalIgnoreCase)))
                        dict["CTN"] = row[columnNames.First(c => c.Equals("CTN", StringComparison.OrdinalIgnoreCase))].ToString();

                    if (columnNames.Any(c => c.Equals("PARTICULAR", StringComparison.OrdinalIgnoreCase)))
                        dict["Particular"] = row[columnNames.First(c => c.Equals("PARTICULAR", StringComparison.OrdinalIgnoreCase))].ToString();

                    if (columnNames.Any(c => c.Equals("BRAND", StringComparison.OrdinalIgnoreCase)))
                        dict["Brand"] = row[columnNames.First(c => c.Equals("BRAND", StringComparison.OrdinalIgnoreCase))].ToString();

                    if (columnNames.Any(c => c.Equals("SERIALRIGHTSTICKERNO", StringComparison.OrdinalIgnoreCase)))
                        dict["SerialStickerNumber"] = row[columnNames.First(c => c.Equals("SERIALRIGHTSTICKERNO", StringComparison.OrdinalIgnoreCase))].ToString();

                    if (columnNames.Any(c => c.Equals("STATUS", StringComparison.OrdinalIgnoreCase)))
                        dict["Status"] = row[columnNames.First(c => c.Equals("STATUS", StringComparison.OrdinalIgnoreCase))].ToString();

                    // Only include Location if it exists
                    if (columnNames.Any(c => c.Equals("LOCATION", StringComparison.OrdinalIgnoreCase)))
                        dict["Location"] = row[columnNames.First(c => c.Equals("LOCATION", StringComparison.OrdinalIgnoreCase))].ToString();

                    return dict;
                })
                .ToList();

            return Json(result);
        }

        public class EditedItemModel
        {
            public int Id { get; set; }
            public string TrackNo { get; set; }
            public string CTN { get; set; }
            public string Particular { get; set; }
            public string Brand { get; set; }
            public string SerialStickerNumber { get; set; }
            public string Status { get; set; }
            public string Location { get; set; }
        }

        [HttpPost]
        public IActionResult SaveEditedItem([FromBody] EditedItemModel model)
        {
            if (model == null)
            {
                return BadRequest("Model is null");
            }

            if (string.IsNullOrWhiteSpace(model.TrackNo))
            {
                return BadRequest("TrackNo is required");
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var schema = new DataTable();
                    using (var adapter = new SqlDataAdapter($"SELECT TOP 0 * FROM [{model.TrackNo}]", conn))
                    {
                        adapter.Fill(schema);
                    }

                    var columnNames = schema.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

                    string safeTableName = model.TrackNo.Replace("'", "''");

                    var queryBuilder = new System.Text.StringBuilder();
                    queryBuilder.Append($"UPDATE [{safeTableName}] SET ");

                    // List to track parameters add
                    var parameters = new List<SqlParameter>();

                    // Add each column if it exists in the table
                    var setStatements = new List<string>();

                    if (columnNames.Any(c => c.Equals("CTN", StringComparison.OrdinalIgnoreCase)))
                    {
                        setStatements.Add("CTN = @CTN");
                        parameters.Add(new SqlParameter("@CTN", (object)model.CTN ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("PARTICULAR", StringComparison.OrdinalIgnoreCase)))
                    {
                        setStatements.Add("PARTICULAR = @Particular");
                        parameters.Add(new SqlParameter("@Particular", (object)model.Particular ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("BRAND", StringComparison.OrdinalIgnoreCase)))
                    {
                        setStatements.Add("BRAND = @Brand");
                        parameters.Add(new SqlParameter("@Brand", (object)model.Brand ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("SERIALRIGHTSTICKERNO", StringComparison.OrdinalIgnoreCase)))
                    {
                        setStatements.Add("SERIALRIGHTSTICKERNO = @SerialStickerNumber");
                        parameters.Add(new SqlParameter("@SerialStickerNumber", (object)model.SerialStickerNumber ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("STATUS", StringComparison.OrdinalIgnoreCase)))
                    {
                        setStatements.Add("STATUS = @Status");
                        parameters.Add(new SqlParameter("@Status", (object)model.Status ?? DBNull.Value));
                    }

                    // Only include Location if it exists in the table
                    if (columnNames.Any(c => c.Equals("LOCATION", StringComparison.OrdinalIgnoreCase)))
                    {
                        setStatements.Add("LOCATION = @Location");
                        parameters.Add(new SqlParameter("@Location", (object)model.Location ?? DBNull.Value));
                    }

                    queryBuilder.Append(string.Join(", ", setStatements));

                    string idColumnName = columnNames.First(c => c.Equals("ID", StringComparison.OrdinalIgnoreCase));
                    queryBuilder.Append($" WHERE {idColumnName} = @Id");
                    parameters.Add(new SqlParameter("@Id", model.Id));

                    string query = queryBuilder.ToString();

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return NotFound($"No record found with ID {model.Id} in table {model.TrackNo}");
                        }
                    }

                    return Ok(new { success = true, message = "Item updated successfully" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveEditedItem: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }
        public class RepairRequestModel
        {
            public int Id { get; set; }
            public int ItemId { get; set; }
            public string TrackNo { get; set; }
            public string Description { get; set; }
            public string Status { get; set; }
            public string CTN { get; set; }
            public string Particular { get; set; }
            public string Brand { get; set; }
            public string SerialStickerNumber { get; set; }
            public string Location { get; set; }
            public DateTime RequestDate { get; set; } = DateTime.Now;
            public string RequestedBy { get; set; }
        }

        [HttpPost]
        public IActionResult RequestRepair([FromBody] RepairRequestModel model)
        {
            if (model == null)
            {
                return BadRequest("Model is null");
            }

            if (string.IsNullOrWhiteSpace(model.TrackNo))
            {
                return BadRequest("TrackNo is required");
            }

            try
            {
                string username = HttpContext.Session.GetString("Username");
                model.RequestedBy = username;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string safeTableName = model.TrackNo.Replace("'", "''");

                    string updateQuery = $@"
                UPDATE [{safeTableName}]
                SET STATUS = 'Pending'
                WHERE ID = @Id";

                    using (var cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", model.Id);
                        cmd.ExecuteNonQuery();
                    }

                    string insertQuery = @"
                INSERT INTO RepairRequests (
                    ItemId, TrackNo, CTN, Particular, Brand, 
                    SerialStickerNumber, Description, Status, 
                    RequestDate, RequestedBy, Location)
                VALUES (
                    @ItemId, @TrackNo, @CTN, @Particular, @Brand, 
                    @SerialStickerNumber, @Description, @Status, 
                    @RequestDate, @RequestedBy, @Location)";

                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", model.ItemId); 
                        cmd.Parameters.AddWithValue("@TrackNo", model.TrackNo);
                        cmd.Parameters.AddWithValue("@CTN", (object)model.CTN ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Particular", (object)model.Particular ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Brand", (object)model.Brand ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SerialStickerNumber", (object)model.SerialStickerNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description", model.Description);
                        cmd.Parameters.AddWithValue("@Status", "Pending");
                        cmd.Parameters.AddWithValue("@RequestDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@RequestedBy", (object)model.RequestedBy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Location", (object)model.Location ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { success = true, message = "Repair request submitted successfully" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RequestRepair: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }


        public IActionResult RepairRequests()
        {
            // Check if user is authorized
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToAction("Index", "Login");
            }

            // Get all pending repair requests
            var pendingRequests = GetPendingRepairRequests();

            return View(pendingRequests);
        }

        private List<RepairRequestModel> GetPendingRepairRequests()
        {
            var requests = new List<RepairRequestModel>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = @"
            SELECT * FROM RepairRequests 
            WHERE Status IN ('Pending', 'Repairing')
            ORDER BY RequestDate DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add(new RepairRequestModel
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                TrackNo = reader.GetString(reader.GetOrdinal("TrackNo")),
                                CTN = reader["CTN"] as string,
                                Particular = reader["Particular"] as string,
                                Brand = reader["Brand"] as string,
                                SerialStickerNumber = reader["SerialStickerNumber"] as string,
                                Description = reader["Description"] as string,
                                Status = reader["Status"] as string,
                                RequestDate = reader.GetDateTime(reader.GetOrdinal("RequestDate")),
                                RequestedBy = reader["RequestedBy"] as string,
                                Location = reader["Location"] as string
                            });
                        }
                    }
                }
            }

            return requests;
        }

        [HttpGet]
        public JsonResult GetRepairRequest(int id)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = "SELECT * FROM RepairRequests WHERE Id = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var request = new
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                    TrackNo = reader["TrackNo"] as string,
                                    CTN = reader["CTN"] as string,
                                    Particular = reader["Particular"] as string,
                                    Brand = reader["Brand"] as string,
                                    SerialStickerNumber = reader["SerialStickerNumber"] as string,
                                    Description = reader["Description"] as string,
                                    Status = reader["Status"] as string,
                                    RequestDate = reader.GetDateTime(reader.GetOrdinal("RequestDate")),
                                    RequestedBy = reader["RequestedBy"] as string,
                                    Location = reader["Location"] as string
                                };

                                return Json(request);
                            }
                        }
                    }

                    Response.StatusCode = 404;
                    return Json(new { error = "Repair request not found" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}
