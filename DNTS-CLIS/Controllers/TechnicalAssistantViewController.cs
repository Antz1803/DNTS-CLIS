﻿using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static DNTS_CLIS.Controllers.TALaboratoryController;

namespace DNTS_CLIS.Controllers
{
    public class TechnicalAssistantViewController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public TechnicalAssistantViewController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DNTS_CLISContext");
        }

        public IActionResult Index(string selectedTrackNo)
        {
            ViewBag.TrackNumbers = GetTrackNumbers();
            ViewBag.SelectedTrackNo = selectedTrackNo;
            ViewBag.LaboratoryName = GetLaboratories();

            if (string.IsNullOrEmpty(selectedTrackNo))
                return View(new DataTable());

            var functionalRecords = GetRecordsByTrackNo(selectedTrackNo, "FUNCTIONAL");
            var defectiveRecords = GetRecordsByTrackNo(selectedTrackNo, "DEFECTIVE");
            var unknownRecords = GetRecordsByTrackNo(selectedTrackNo, "UNKNOWN");

            ViewBag.FunctionalRecords = functionalRecords;
            ViewBag.DefectiveRecords = defectiveRecords;
            ViewBag.UnknownRecords = unknownRecords;

            return View(GetTableData(selectedTrackNo));
        }
        private List<string> GetLaboratories()
        {
            var laboratories = new List<string>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT LaboratoryName FROM Laboratories", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) laboratories.Add(reader.GetString(0));

            return laboratories
            .OrderBy(lab => ExtractLabNumber(lab))
            .ThenBy(lab => lab)
            .ToList();
        }
        private int ExtractLabNumber(string labName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(labName, @"\d+");
            return match.Success ? int.Parse(match.Value) : int.MaxValue;
        }
        public IActionResult AssignHistory()
        {
            ViewBag.Laboratories = GetLaboratories();
            return View();
        }
        [HttpGet]
        public JsonResult GetAssignedTracks(string laboratoryName)
        {
            var trackNumbers = new List<string>();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT TrackNo FROM AssignedLaboratories WHERE LaboratoryName = @LabName", conn);
            cmd.Parameters.AddWithValue("@LabName", laboratoryName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                trackNumbers.Add(reader.GetString(0));
            }

            Console.WriteLine($"Tracks for {laboratoryName}: {string.Join(", ", trackNumbers)}");

            return Json(trackNumbers);
        }
        [HttpGet]
        public JsonResult GetTableDataOne(string trackNo)
        {
            var dataTable = new DataTable();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var adapter = new SqlDataAdapter($"SELECT * FROM [{trackNo}]", conn);
            adapter.Fill(dataTable);

            Console.WriteLine($"Rows fetched for {trackNo}: {dataTable.Rows.Count}");

            if (dataTable.Rows.Count == 0)
            {
                Console.WriteLine($"No data found for Track No: {trackNo}");
            }

            var result = new List<Dictionary<string, object>>();
            foreach (DataRow row in dataTable.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    dict[col.ColumnName] = row[col] ?? DBNull.Value;
                }
                result.Add(dict);
            }

            Console.WriteLine($"Data Sent: {System.Text.Json.JsonSerializer.Serialize(result)}");

            return Json(result);
        }


        [HttpPost]
        public IActionResult AssignLaboratory(string selectedTrackNo, string selectedIDLaboratories)
        {
            if (!string.IsNullOrEmpty(selectedTrackNo) && !string.IsNullOrEmpty(selectedIDLaboratories))
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Ensure the AssignedLaboratories table exists
                string createTableQuery = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AssignedLaboratories')
                BEGIN
                    CREATE TABLE AssignedLaboratories (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TrackNo NVARCHAR(255) UNIQUE,
                        LaboratoryName NVARCHAR(255),
                    )
                END";
                using var createCmd = new SqlCommand(createTableQuery, conn);
                createCmd.ExecuteNonQuery();

                // Insert or update the assigned laboratory
                string upsertQuery = @"
                IF EXISTS (SELECT 1 FROM AssignedLaboratories WHERE TrackNo = @TrackNo)
                    UPDATE AssignedLaboratories 
                    SET LaboratoryName = @Laboratory
                    WHERE TrackNo = @TrackNo
                ELSE
                    INSERT INTO AssignedLaboratories (TrackNo, LaboratoryName) 
                    VALUES (@TrackNo, @Laboratory)";

                using var cmd = new SqlCommand(upsertQuery, conn);
                cmd.Parameters.AddWithValue("@TrackNo", selectedTrackNo);
                cmd.Parameters.AddWithValue("@Laboratory", selectedIDLaboratories);
                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "Track number successfully assigned to the laboratory.";
            }
            else
            {
                TempData["ErrorMessage"] = "Please select a valid Track No. and Laboratory.";
            }

            return RedirectToAction("Index", new { selectedTrackNo });
        }


        private List<string> GetTrackNumbers()
        {
            var trackNumbers = new List<string>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Fetch only tables that contain "TA" in their names
            using var cmd = new SqlCommand(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%TA%'", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) trackNumbers.Add(reader.GetString(0));

            return trackNumbers;
        }

        private DataTable GetTableData(string trackNo)
        {
            var dataTable = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var adapter = new SqlDataAdapter($"SELECT * FROM [{trackNo}]", conn);
            adapter.Fill(dataTable);

            return dataTable;
        }

        private DataTable GetRecordsByTrackNo(string trackNo, string status)
        {
            var dataTable = new DataTable();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = $"SELECT * FROM [{trackNo}] WHERE STATUS = @status";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@status", status);

            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(dataTable);

            return dataTable;
        }

        private void ExecuteNonQuery(string query, params SqlParameter[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddRange(parameters);
            cmd.ExecuteNonQuery();
        }
        [HttpPost]
        public IActionResult UpdateRow(string trackNo, int rowId, Dictionary<string, string> updatedValues)
        {
            if (string.IsNullOrEmpty(trackNo) || rowId == 0 || updatedValues == null || updatedValues.Count == 0)
            {
                return BadRequest("Invalid data provided.");
            }

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Generate the update query dynamically
            var setClauses = string.Join(", ", updatedValues.Keys.Select(col => $"[{col}] = @{col}"));
            string updateQuery = $"UPDATE [{trackNo}] SET {setClauses} WHERE ID = @ID";

            using var cmd = new SqlCommand(updateQuery, conn);
            foreach (var item in updatedValues)
            {
                cmd.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value.ToString());
            }
            cmd.Parameters.AddWithValue("@ID", rowId);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                TempData["SuccessMessage"] = "Record updated successfully.";
                return RedirectToAction("Index", new { selectedTrackNo = trackNo });
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update record.";
                return RedirectToAction("Index", new { selectedTrackNo = trackNo });
            }
        }
        [HttpPost]
        public IActionResult DeleteRow(string trackNo, int rowId)
        {
            if (string.IsNullOrEmpty(trackNo) || rowId == 0)
            {
                TempData["ErrorMessage"] = "Invalid Track No or Row ID.";
                return RedirectToAction("Index", new { selectedTrackNo = trackNo });
            }

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string deleteQuery = $"DELETE FROM [{trackNo}] WHERE ID = @ID";

            using var cmd = new SqlCommand(deleteQuery, conn);
            cmd.Parameters.AddWithValue("@ID", rowId);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                TempData["SuccessMessage"] = "Record deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete record.";
            }

            return RedirectToAction("Index", new { selectedTrackNo = trackNo });
        }
        [HttpPost]
        public IActionResult DeleteTrackTable(string trackNo)
        {
            if (string.IsNullOrEmpty(trackNo))
            {
                TempData["ErrorMessage"] = "Invalid Track No.";
                return RedirectToAction("Index");
            }

            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DNTS_CLISContext")))
                {
                    connection.Open();

                    // First, delete the record from AssignedLaboratories
                    string deleteAssignmentQuery = "DELETE FROM AssignedLaboratories WHERE TrackNo = @TrackNo";
                    using (var deleteAssignmentCmd = new SqlCommand(deleteAssignmentQuery, connection))
                    {
                        deleteAssignmentCmd.Parameters.AddWithValue("@TrackNo", trackNo);
                        deleteAssignmentCmd.ExecuteNonQuery();
                    }

                    // Then, drop the corresponding track table
                    string deleteTableQuery = $"DROP TABLE [{trackNo}]";
                    using (var deleteTableCmd = new SqlCommand(deleteTableQuery, connection))
                    {
                        deleteTableCmd.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = $"TrackNo '{trackNo}' and its assignment have been deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting TrackNo: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public JsonResult GetMoveHistory(string serialNumber)
        {
            if (string.IsNullOrEmpty(serialNumber))
                return Json(new { success = false, message = "Serial number is required." });

            try
            {
                var moveHistory = new List<object>();

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                string query = @"
            SELECT 
                di.TodayDate,
                di.[From],
                di.[To],
                di.Purpose,
                di.RequestedBy
            FROM DeploymentInfos di
            INNER JOIN DeployItems dep ON di.Id = dep.DeploymentInfoId
            WHERE dep.SerialControlNumber = @SerialNumber
            ORDER BY di.TodayDate DESC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@SerialNumber", serialNumber);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    moveHistory.Add(new
                    {
                        date = reader["TodayDate"] != DBNull.Value ? Convert.ToDateTime(reader["TodayDate"]) : (DateTime?)null,
                        from = reader["From"]?.ToString() ?? null,
                        to = reader["To"]?.ToString() ?? null,
                        purpose = reader["Purpose"]?.ToString() ?? null,
                        requestedBy = reader["RequestedBy"]?.ToString() ?? null
                    });
                }

                return Json(new { success = true, history = moveHistory });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error fetching move history: {ex.Message}" });
            }
        }
        [HttpPost]
        public IActionResult AddNewItem([FromBody] NewItemModel model)
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

                    // Get table schema to check columns
                    var schema = new DataTable();
                    using (var adapter = new SqlDataAdapter($"SELECT TOP 0 * FROM [{model.TrackNo}]", conn))
                    {
                        adapter.Fill(schema);
                    }

                    var columnNames = schema.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                    string safeTableName = model.TrackNo.Replace("'", "''");

                    // Build insert query based on existing columns
                    var queryBuilder = new System.Text.StringBuilder();
                    queryBuilder.Append($"INSERT INTO [{safeTableName}] (");

                    var columns = new List<string>();
                    var paramNames = new List<string>();
                    var parameters = new List<SqlParameter>();

                    // Add each column if it exists in the table
                    if (columnNames.Any(c => c.Equals("CTN", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("CTN");
                        paramNames.Add("@CTN");
                        parameters.Add(new SqlParameter("@CTN", (object)model.CTN ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("PARTICULAR", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("PARTICULAR");
                        paramNames.Add("@Particular");
                        parameters.Add(new SqlParameter("@Particular", (object)model.Particular ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("DATEOFACQUISITION", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("DATEOFACQUISITION");
                        paramNames.Add("@Dateofacquisition");
                        parameters.Add(new SqlParameter("@Dateofacquisition", (object)model.DateOfAcquisition ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("BRAND", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("BRAND");
                        paramNames.Add("@Brand");
                        parameters.Add(new SqlParameter("@Brand", (object)model.Brand ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("SERIALSTICKERNO", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("SERIALSTICKERNO");
                        paramNames.Add("@SerialStickerNumber");
                        parameters.Add(new SqlParameter("@SerialStickerNumber", (object)model.SerialStickerNumber ?? DBNull.Value));
                    }

                    if (columnNames.Any(c => c.Equals("STATUS", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("STATUS");
                        paramNames.Add("@Status");
                        parameters.Add(new SqlParameter("@Status", (object)model.Status ?? DBNull.Value));
                    }

                    // Only include Location if it exists in the table
                    if (columnNames.Any(c => c.Equals("LOCATION", StringComparison.OrdinalIgnoreCase)))
                    {
                        columns.Add("LOCATION");
                        paramNames.Add("@Location");
                        parameters.Add(new SqlParameter("@Location", (object)model.Location ?? DBNull.Value));
                    }

                    queryBuilder.Append(string.Join(", ", columns));
                    queryBuilder.Append(") VALUES (");
                    queryBuilder.Append(string.Join(", ", paramNames));
                    queryBuilder.Append(")");

                    string query = queryBuilder.ToString();

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                        cmd.ExecuteNonQuery();
                    }

                    return Ok(new { success = true, message = "Item added successfully" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddNewItem: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }
    }
}