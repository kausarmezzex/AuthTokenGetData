using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;

namespace AuthTokenGetData.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DataController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("feed")]
        [Authorize]
        public IActionResult GetAllData()
        {
            DataTable dataTable = new DataTable();
            string connectionString = _configuration.GetConnectionString("BloggieDConnectionString");

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                try
                {
                    con.Open();
                    string query = @"SELECT TOP 10 
                                        ProductId, 
                                        SKU AS SKUId, 
                                        Barcode AS EAN, 
                                        dbo.getbrandname(p.ProductID) AS Brand, 
                                        dbo.getcategoryname(p.ProductID) AS Category, 
                                        p.MainProductId, 
                                        '' AS StockStatus, 
                                        (SELECT SUM(ISNULL(Inventory, 0)) 
                                         FROM ProductVariant 
                                         WHERE MainProductId = p.MainProductId 
                                           AND Deleted = 0 
                                           AND IsDefault = 1) AS StockQuantity, 
                                        p.Name AS Title, 
                                        Description, 
                                        0 AS NormalPriceWithoutVAT, 
                                        0 AS NormalPriceWithVAT, 
                                        TC.Name AS VatRate 
                                     FROM product p 
                                     JOIN TaxClass TC ON TC.TaxClassID = p.TaxClassID 
                                     WHERE p.Deleted = 0 
                                       AND ProductId IN (SELECT ProductID FROM ProductCategory WHERE CategoryID = 526);";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dataTable);
                        }
                    }

                    var dataList = new List<Dictionary<string, object>>();
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var dataObject = new Dictionary<string, object>();
                        foreach (DataColumn col in dataTable.Columns)
                        {
                            if (col.ColumnName == "MainProductId")
                            {
                                var mainProductId = row[col].ToString();
                                string imageUrl = GetImageUrl(mainProductId);
                                dataObject["Imageurl1"] = imageUrl;
                            }
                            else
                            {
                                dataObject[col.ColumnName] = row[col];
                            }
                        }
                        dataList.Add(dataObject);
                    }

                    return Ok(dataList);
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error: {ex.Message}");
                }
            }
        }

        private string GetImageUrl(string mainProductId)
        {
            var formats = new[] { "jpg", "png", "webp" };
            foreach (var format in formats)
            {
                string url = $"https://travelbookplus.com/images/Product/large/{mainProductId}.{format}";
                if (UrlExists(url))
                {
                    return url;
                }
            }
            return $"https://travelbookplus.com/images/Product/large/default.jpg";
        }

        private bool UrlExists(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "HEAD";
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
