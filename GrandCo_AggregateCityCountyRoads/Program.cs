using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace GrandCo_AggregateCityCountyRoads
{
    class Program
    {
        static void Main(string[] args)
        {
            int intJurisOID;
            string strJurisCentroid;
            int intRowCountUtransSegsWithBuffer;
            int intDistBtwnPnts;
            string strMatchOID = string.Empty;
            bool blnMatchedOID = false;
            string strJurisFullName = string.Empty;
            string strMatchFullName = string.Empty;
            long lngMatch_L_F_ADD;
            long lngMatch_L_T_ADD;
            long lngMatch_R_F_ADD;
            long lngMatch_R_T_ADD;
            int intMatchCentroidDist = 0;
            string strUtransCentroid;
            string strJurisStartPnt = string.Empty;
            string strJurisEndPnt = string.Empty;
            string strUtransStartPnt = string.Empty;
            string strUtransEndPnt = string.Empty;
            
            try
            {
                //string strMonthDay = string.Concat(DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Year);
                //setup a file stream and a stream writer to write out the addresses that do not have a nearby street or a street out of range
                string path = @"C:\temp\MoabCity" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm") +".txt";
                FileStream fileStream = new FileStream(path, FileMode.Create);
                StreamWriter streamWriter = new StreamWriter(fileStream);
                streamWriter.WriteLine("UniqueID" + "," + "CentroidDist" + "," + "MatchOID" + "," + "JurisOID" + "," + "ChangeType" + "," + "Notes" + "," + "UtransFullname" + "," + "JurisFullname");
                int intUniqueID = 0;

                //SqlConnection connection = new SqlConnection(GrandCo_AggregateCityCountyRoads.Properties.Settings.Default.ConnectionString);
                var connectionString = ConfigurationManager.AppSettings["myConn"];
                //test

                //////////// GET A RECORD SET OF THE JURIS' SEGMENTS TO LOOP THROUGH ////////////////////////////////////////////// 
                using (SqlConnection con1 = new SqlConnection(connectionString))
                {
                    // Open the SqlConnection.
                    con1.Open();

                    // The following code uses an SqlCommand based on the SqlConnection.
                    //using (SqlCommand command1 = new SqlCommand("SELECT * FROM MOAB_CITY_SUB;", con1))
                    using (SqlCommand command1 = new SqlCommand("SELECT * FROM MOAB_CITY_SUB where OBJECTID_12 < 100", con1))

                    using (SqlDataReader readerJurisSegment = command1.ExecuteReader())
                    {
                        if (readerJurisSegment.HasRows)
                        {
                            // loop through the jurisdiction's road segments
                            while (readerJurisSegment.Read())
                            {
                                //Console.WriteLine(reader1.GetInt32(0));
                                //Console.WriteLine(reader1["OBJECTID"]);
                                //Console.WriteLine(reader.GetString(3));
                                blnMatchedOID = false;
                                intJurisOID = 0;
                                intJurisOID = Convert.ToInt32(readerJurisSegment["OBJECTID_12"]);
                                intDistBtwnPnts = 1000000; // set this to one million each time we loop through a new juris' segment - will be used later to see what the closest utrans centroid is to this juris segment
                                intRowCountUtransSegsWithBuffer = 0;


                                //////////// BUFFER THE CURRENT SEGMENT AND SELECT ALL UTRANS SEGMENTS THAT FALL WITHIN THE BUFFER ////////////////////////////////////////////// 
                                //call the stored procedure get the nearest/closest address from the current ADDRPNTS_FROMCOUNTY point
                                using (SqlConnection con2 = new SqlConnection(connectionString))
                                {
                                    // opne the sql connection to check for utrans match (buffers the juris's segment and then selects-within)
                                    con2.Open();

                                    // create a second command
                                    using (SqlCommand command2 = new SqlCommand("uspBufferFeatureSelectUtransWithin", con2))
                                    {
                                        // set the command object so it knows to execute a stored procedure
                                        command2.CommandType = CommandType.StoredProcedure;

                                        // add parameter to command, which will be passed to the stored procedure
                                        command2.Parameters.Add(new SqlParameter("@pBuffDist", 80));
                                        command2.Parameters.Add(new SqlParameter("@pOID", Convert.ToInt32(readerJurisSegment["OBJECTID_12"])));

                                        // execute command2
                                        using (SqlDataReader readerUtransWithinBuffer = command2.ExecuteReader())
                                        {
                                            if (readerUtransWithinBuffer.HasRows)
                                            {
                                                #region get row count 
                                                // we now know it has row (reader2.HasRows), so let's see how many....
                                                ////////////// GET A ROW COUNT OF SEGMENTS THAT FALL WITHIN THE JURIS' BUFFER... BEGIN ///////////////////////////////////////////
                                                // call another stored procedure to get a row count for how many records were returned for this stored 
                                                using (SqlConnection conRowCount = new SqlConnection(connectionString))
                                                {
                                                    conRowCount.Open();
                                                    using (SqlCommand commandRowCount = new SqlCommand("uspGetRowCountForBuffUtransWithin", conRowCount))
                                                    {
                                                        commandRowCount.CommandType = CommandType.StoredProcedure;
                                                        // add parameter to command, which will be passed to the stored procedure
                                                        commandRowCount.Parameters.Add(new SqlParameter("@pBuffDist", 80));
                                                        commandRowCount.Parameters.Add(new SqlParameter("@pOID", Convert.ToInt32(readerJurisSegment["OBJECTID_12"])));

                                                        // execute the row count stored procedure
                                                        using (SqlDataReader readerRowCount = commandRowCount.ExecuteReader())
                                                        {
                                                            if (readerRowCount.HasRows)
                                                            {
                                                                 while (readerRowCount.Read())
                                                                {
                                                                    intRowCountUtransSegsWithBuffer = Convert.ToInt32(readerRowCount["count"]);
                                                                }                                                               
                                                            }
                                                            else
                                                            {
                                                                Console.WriteLine("uspGetRowCountForBuffUtransWithin returned without any rows for objectid_12: " + readerJurisSegment["OBJECTID_12"].ToString());
                                                            }

                                                        }
                                                    }
                                                } ////////////// GET A ROW COUNT OF SEGMENTS THAT FALL WITHIN THE JURIS' BUFFER... END ///////////////////////////////////////////
                                                #endregion

                                                // iterate through the utrans segments that are within the juris's buffer
                                                while (readerUtransWithinBuffer.Read())
                                                {
                                                    // if the select-within-buffer returned more than one utrans segment, then....
                                                    if (intRowCountUtransSegsWithBuffer == 1)
                                                    {

                                                        // clear out variables before assignment
                                                        strMatchOID = string.Empty;
                                                        strMatchFullName = string.Empty;
                                                        strJurisFullName = string.Empty;
                                                        lngMatch_L_F_ADD = 0;
                                                        lngMatch_L_T_ADD = 0;
                                                        lngMatch_R_F_ADD = 0;
                                                        lngMatch_R_T_ADD = 0;

                                                        // check the attributes to see if the fullname's are alike
                                                        // first check if the current segment has zero ranges, if so... don't compare them
                                                        // then check the fullnames to see if they are alike
                                                        //now check if the fullnames are alike, if they are mark this one as a potential winner
                                                        //if (readerUtransWithinBuffer["FULLNAME"].ToString().Contains(readerJurisSegment["ROAD_NAME"].ToString()))
                                                        //if (true)
                                                        
                                                        string strCompareUtransStreetName = readerUtransWithinBuffer["STREETNAME"].ToString().ToUpper();
                                                        string strCompareJurisFullName = readerJurisSegment["ROAD_NAME"].ToString().ToUpper();
                                                        if (strCompareJurisFullName.Contains(strCompareUtransStreetName))
                                                        {
                                                            // do nothing... the segment found a juris segment found a match in the utrans database - exit loop and move onto the next juris' segment
                                                            break;
                                                        }
                                                        else // the road segment's name did not match the targeted utrans segment
                                                        {
                                                            // now check the angle of the line to see if it's running the same direction - if it's not then it's most-likley a true different road segment

                                                            #region getJurisStartEndPnt
                                                            // get the start and end point for the juris segment - uspGetJurisStartEndPnts
                                                            using (SqlConnection conGetJurisStartEndPnt = new SqlConnection(connectionString))
                                                            {
                                                                // Open the SqlConnection.
                                                                conGetJurisStartEndPnt.Open();

                                                                 // create a sql command
                                                                using (SqlCommand commandGetJurisStartEndPnts = new SqlCommand("uspGetJurisStartEndPnts", conGetJurisStartEndPnt))
                                                                {
                                                                    // set the command object so it knows to execute a stored procedure
                                                                    commandGetJurisStartEndPnts.CommandType = CommandType.StoredProcedure;

                                                                    // add parameter to sql command, which will be passed to the stored procedure
                                                                    commandGetJurisStartEndPnts.Parameters.Add(new SqlParameter("@OID", Convert.ToInt32(readerJurisSegment["OBJECTID_12"])));

                                                                    // execute the sql command
                                                                    using (SqlDataReader readerGetJurisStartEndPnts = commandGetJurisStartEndPnts.ExecuteReader())
                                                                    {
                                                                        if (readerGetJurisStartEndPnts.HasRows)
                                                                        {
                                                                            // null out the sring from previous values
                                                                            strJurisStartPnt = string.Empty;
                                                                            strJurisEndPnt = string.Empty;

                                                                            // iterate through the sql data reader now that we know it has rows....
                                                                            while (readerGetJurisStartEndPnts.Read())
                                                                            {
                                                                                strJurisStartPnt = readerGetJurisStartEndPnts["myStartPoint"].ToString();
                                                                                strJurisEndPnt = readerGetJurisStartEndPnts["myEndPoint"].ToString();
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            #endregion

                                                            // get the start and end point for the utrans segment - uspGetUtransStartEndPnts
                                                            #region getUtransStartEndPnt
                                                            // get the start and end point for the juris segment - uspGetJurisStartEndPnts
                                                            using (SqlConnection conGetUtransStartEndPnt = new SqlConnection(connectionString))
                                                            {
                                                                // Open the SqlConnection.
                                                                conGetUtransStartEndPnt.Open();

                                                                // create a sql command
                                                                using (SqlCommand commandGetUtransStartEndPnts = new SqlCommand("uspGetJurisStartEndPnts", conGetUtransStartEndPnt))
                                                                {
                                                                    // set the command object so it knows to execute a stored procedure
                                                                    commandGetUtransStartEndPnts.CommandType = CommandType.StoredProcedure;

                                                                    // add parameter to sql command, which will be passed to the stored procedure
                                                                    commandGetUtransStartEndPnts.Parameters.Add(new SqlParameter("@OID", Convert.ToInt32(readerUtransWithinBuffer["OBJECTID"])));

                                                                    // execute the sql command
                                                                    using (SqlDataReader readerGetUtransStartEndPnts = commandGetUtransStartEndPnts.ExecuteReader())
                                                                    {
                                                                        if (readerGetUtransStartEndPnts.HasRows)
                                                                        {
                                                                            // null out the sring from previous values
                                                                            strUtransStartPnt = string.Empty;
                                                                            strUtransEndPnt = string.Empty;

                                                                            // iterate through the sql data reader now that we know it has rows....
                                                                            while (readerGetUtransStartEndPnts.Read())
                                                                            {
                                                                                strUtransStartPnt = readerGetUtransStartEndPnts["myStartPoint"].ToString();
                                                                                strUtransEndPnt = readerGetUtransStartEndPnts["myEndPoint"].ToString();
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            #endregion


                                                            // parse the start and end point strings from the sql data reader into x and y values - to pass into the line direction method
                                                            // data comes back from sql query like this: POINT (626558.21700000018 4269049.5782999992)
                                                            double dblJurisStartX = getX_fromPnt(strJurisStartPnt);
                                                            double dblJurisEndX = getX_fromPnt(strJurisEndPnt);
                                                            double dblJurisStartY = getY_fromPnt(strJurisStartPnt);
                                                            double dblJurisEndY = getY_fromPnt(strJurisEndPnt);

                                                            double dblUtransStartX = getX_fromPnt(strUtransStartPnt);
                                                            double dblUtransEndX = getX_fromPnt(strUtransEndPnt);
                                                            double dblUtransStartY = getX_fromPnt(strUtransStartPnt);
                                                            double dblUtransEndY = getX_fromPnt(strUtransEndPnt);


                                                            // call the line direction method to see if line segment is of similar angle
                                                            // expected parameters: getLineDirection(double dblEndY, double dblStartY, double dblEndX, double dblStartX)
                                                            double dblLineDirectionUtrans = getLineDirection(dblUtransEndY, dblUtransStartY, dblUtransEndX, dblUtransStartX);
                                                            double dblLineDirectionJuris = getLineDirection(dblJurisEndY, dblJurisStartY, dblJurisEndX, dblJurisStartX);

                                                            // check if the line directions are simlar (within a certain angle degrees - maybe 45?)
                                                            // check if the matched utrans segment's angle is within 45 degrees in each direction
                                                            // strait north bearing street is 360 degrees, south bearing segment is 180
                                                            double dblJurisLineAngleLowEnd = dblLineDirectionJuris - 90;
                                                            double dblJurisLineAngleHighEnd = dblLineDirectionJuris + 90;

                                                            if (dblLineDirectionUtrans > dblJurisLineAngleLowEnd & dblLineDirectionUtrans < dblJurisLineAngleHighEnd)
                                                            {
                                                                // if this is true then assign the dist variable this new value - for comparison next loop through
                                                                // set attribute variables for this utrans segments - in case it's the winner of the closest dist competition
                                                                strMatchOID = readerUtransWithinBuffer["OBJECTID"].ToString();
                                                                blnMatchedOID = true;
                                                                strJurisFullName = readerJurisSegment["ROAD_NAME"].ToString().ToUpper();
                                                                strMatchFullName = readerUtransWithinBuffer["STREETNAME"].ToString().ToUpper();
                                                                lngMatch_L_F_ADD = Convert.ToInt64(readerUtransWithinBuffer["L_F_ADD"]);
                                                                lngMatch_L_T_ADD = Convert.ToInt64(readerUtransWithinBuffer["L_T_ADD"]);
                                                                lngMatch_R_F_ADD = Convert.ToInt64(readerUtransWithinBuffer["R_F_ADD"]);
                                                                lngMatch_R_T_ADD = Convert.ToInt64(readerUtransWithinBuffer["R_T_ADD"]);

                                                                intUniqueID = intUniqueID + 1;
                                                                streamWriter.WriteLine(intUniqueID + "," + "-1," + strMatchOID + "," + readerJurisSegment["OBJECTID_12"].ToString() + "," + "ATTRIBUTE," + "Segmant found but different attributes," + strMatchFullName + "," + strJurisFullName);
                                                                break;                                                          
                                                            }
                                                            else // if it's out of the range then let's say it's a differnt road segment (with different street name) all together
                                                            {

                                                            }
                                                        }




                                                    }
                                                    else // more than one utrans segment was selected within the buffer - loop through each
                                                    {
                                                        #region get juris's centroid
                                                        ////////////// GET JURIS CENTROID... BEGIN ////////////////////////////////////////////////////////////////////////////////////
                                                        using (SqlConnection conGetJurisCentroid = new SqlConnection(connectionString))
                                                        {
                                                            // opne the sql connection to check for utrans match (buffers the juris's segment and then selects-within)
                                                            conGetJurisCentroid.Open();

                                                             // create a command
                                                            using (SqlCommand commandGetJurisCentroid = new SqlCommand("uspGetJurisCentroid", conGetJurisCentroid))
                                                            {
                                                                // set the command object so it knows to execute a stored procedure
                                                                commandGetJurisCentroid.CommandType = CommandType.StoredProcedure;

                                                                // add parameter to command, which will be passed to the stored procedure
                                                                commandGetJurisCentroid.Parameters.Add(new SqlParameter("@pBuff", 10));
                                                                commandGetJurisCentroid.Parameters.Add(new SqlParameter("@pOID", Convert.ToInt32(readerJurisSegment["OBJECTID_12"])));

                                                                 // execute command2
                                                                using (SqlDataReader readerGetJurisCentroid = commandGetJurisCentroid.ExecuteReader())
                                                                {
                                                                    strJurisCentroid = string.Empty;

                                                                    if (readerGetJurisCentroid.HasRows)
                                                                    {
                                                                        // iterate through results, printing each to console
                                                                        while (readerGetJurisCentroid.Read())
                                                                        {
                                                                            // get the centroid of the current (in loop) utrans segment that is within the select-within buffer
                                                                            strJurisCentroid = readerGetJurisCentroid.GetString(0);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        Console.WriteLine("error with uspGetJurisCentroid stored procedure and the 2nd parameter of: " + Convert.ToInt32(readerJurisSegment["OBJECTID_12"]));
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        ////////////// GET JURIS CENTROID... BEGIN ////////////////////////////////////////////////////////////////////////////////////
                                                        #endregion

                                                        ////////////// GET CENTROID OF UTRANS SEGMENT IN THE BUFFER ///////////////////////////////////////////
                                                        // get centroid's of utrans segments retruned
                                                        using (SqlConnection conGetCentroids = new SqlConnection(connectionString))
                                                        {
                                                            strUtransCentroid = string.Empty;

                                                            // opne the sql connection to check for utrans match (buffers the juris's segment and then selects-within)
                                                            conGetCentroids.Open();

                                                            // create a second command
                                                            using (SqlCommand commandGetCentroids = new SqlCommand("uspGetUtransCentroid", conGetCentroids))
                                                            {
                                                                // set the command object so it knows to execute a stored procedure
                                                                commandGetCentroids.CommandType = CommandType.StoredProcedure;

                                                                // add parameter to command, which will be passed to the stored procedure
                                                                commandGetCentroids.Parameters.Add(new SqlParameter("@pBuff", 10));
                                                                commandGetCentroids.Parameters.Add(new SqlParameter("@pOID", Convert.ToInt32(readerUtransWithinBuffer["OBJECTID"])));

                                                                // execute command2
                                                                using (SqlDataReader readerGetCentroids = commandGetCentroids.ExecuteReader())
                                                                {
                                                                    if (readerGetCentroids.HasRows)
                                                                    {
                                                                        // iterate through results, printing each to console
                                                                        while (readerGetCentroids.Read())
                                                                        {
                                                                            // get the centroid of the current (in loop) utrans segment that is within the select-within buffer
                                                                            strUtransCentroid = readerGetCentroids.GetString(0);


                                                                            #region get dist between points
                                                                            ////////////// GET DIST BETWEEN POINTS... BEGIN ////////////////////////////////////////////////////////////////////////////////////
                                                                            // check the distance between this utran segment's centroid and the juris's centroid and keep the closest one
                                                                            using (SqlConnection conGetDistBtwnPnts = new SqlConnection(connectionString))
                                                                            {
                                                                                // Open the SqlConnection.
                                                                                conGetDistBtwnPnts.Open();

                                                                                // create a command to get the distance between the points
                                                                                using (SqlCommand commandGetDistBtwnPnts = new SqlCommand("uspGetDistBtwTwoPoints", conGetDistBtwnPnts))
                                                                                {
                                                                                    // set the command object so it knows to execute a stored procedure
                                                                                    commandGetDistBtwnPnts.CommandType = CommandType.StoredProcedure;

                                                                                    // clear out variables before assignment
                                                                                    strMatchOID = string.Empty;
                                                                                    strMatchFullName = string.Empty;
                                                                                    strJurisFullName = string.Empty;
                                                                                    intMatchCentroidDist = 0;
                                                                                    lngMatch_L_F_ADD = 0;
                                                                                    lngMatch_L_T_ADD = 0;
                                                                                    lngMatch_R_F_ADD = 0;
                                                                                    lngMatch_R_T_ADD = 0;

                                                                                    commandGetDistBtwnPnts.Parameters.Add(new SqlParameter("@pnt1", strUtransCentroid));
                                                                                    commandGetDistBtwnPnts.Parameters.Add(new SqlParameter("@pnt2", strJurisCentroid));

                                                                                    // execute command2
                                                                                    using (SqlDataReader readerGetDistBtwnPnts = commandGetDistBtwnPnts.ExecuteReader())
                                                                                    {
                                                                                        if (readerGetDistBtwnPnts.HasRows)
                                                                                        {
                                                                                            // iterate through results, printing each to console
                                                                                            while (readerGetDistBtwnPnts.Read())
                                                                                            {
                                                                                                // check if this dist is less then the last dist
                                                                                                if (Convert.ToInt64(readerGetDistBtwnPnts.GetValue(0)) < intDistBtwnPnts)
	                                                                                            {
                                                                                                    //now check if the fullnames are alike, if they are mark this one as a potential winner
                                                                                                    //if (readerUtransWithinBuffer["FULLNAME"].ToString().Contains(readerJurisSegment["ROAD_NAME"].ToString()))
                                                                                                    //if (true)
                                                                                                    string strCompareUtransStreetName = readerUtransWithinBuffer["STREETNAME"].ToString().ToUpper();
                                                                                                    string strCompareJurisFullName = readerJurisSegment["ROAD_NAME"].ToString().ToUpper();
                                                                                                    if (strCompareJurisFullName.Contains(strCompareUtransStreetName))
                                                                                                    {
                                                                                                        // if this is true then assign the dist variable this new value - for comparison next loop through
                                                                                                        intDistBtwnPnts = Convert.ToInt32(readerGetDistBtwnPnts.GetValue(0));

                                                                                                        // set attribute variables for this utrans segments - in case it's the winner of the closest dist competition
                                                                                                        strMatchOID = readerUtransWithinBuffer["OBJECTID"].ToString();
                                                                                                        blnMatchedOID = true;
                                                                                                        strJurisFullName = readerJurisSegment["ROAD_NAME"].ToString().ToUpper();
                                                                                                        strMatchFullName = readerUtransWithinBuffer["STREETNAME"].ToString().ToUpper();
                                                                                                        lngMatch_L_F_ADD = Convert.ToInt64(readerUtransWithinBuffer["L_F_ADD"]);
                                                                                                        lngMatch_L_T_ADD = Convert.ToInt64(readerUtransWithinBuffer["L_T_ADD"]);
                                                                                                        lngMatch_R_F_ADD = Convert.ToInt64(readerUtransWithinBuffer["R_F_ADD"]);
                                                                                                        lngMatch_R_T_ADD = Convert.ToInt64(readerUtransWithinBuffer["R_T_ADD"]);
                                                                                                        intMatchCentroidDist = Convert.ToInt32(readerGetDistBtwnPnts.GetValue(0));
                                                                                                    }
	                                                                                            }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            } //////////////// GET DIST BETWEEN POINTS... END ////////////////////////////////////////////////////////////////////////////////////
                                                                            #endregion

                                                                        //assign these variables to report in the text file the streetnames that did not match
                                                                        strJurisFullName = readerJurisSegment["ROAD_NAME"].ToString().ToUpper();
                                                                        strMatchFullName = readerUtransWithinBuffer["STREETNAME"].ToString().ToUpper();


                                                                        } // end of while-statement - get utrans centroid
                                                                    }
                                                                }
                                                            }
                                                        }


                                                    } // end of more than one utrans selected in the buffer


                                                } // end of while-statement that loops through the selected (within buffer) utrans segments
                                                
                                                // write line here for the utrans seg that had the shortest centroid dists
                                                if (intRowCountUtransSegsWithBuffer != 1)
                                                {
                                                    if (blnMatchedOID == true)
                                                    {
                                                        //intUniqueID = intUniqueID + 1;
                                                        //streamWriter.WriteLine(intUniqueID + "," + intDistBtwnPnts + "," + intMatchCentroidDist + "," + intJurisOID + "," + "Found" + "," + "FoundMatchIdStreetNameFromCentroid" + "," + strMatchFullName + "," + strJurisFullName);
                                                        //break;
                                                    }
                                                    else
                                                    {
                                                        // it might be a new segment
                                                        intUniqueID = intUniqueID + 1;
                                                        if (strMatchOID != string.Empty)
                                                        {
                                                            streamWriter.WriteLine(intUniqueID + "," + "-1" + "," + strMatchOID + "," + intJurisOID + "," + "NEW_MAYBE," + "CentroidStreetNameNotMatched" + "," + strMatchFullName + "," + strJurisFullName);
                                                            //break;
                                                        }
                                                        else
                                                        {
                                                            streamWriter.WriteLine(intUniqueID + "," + "-1," +  "0," + intJurisOID + "," + "NEW_MAYBE," + "CentroidStreetNameNotMatched" + "," + strMatchFullName + "," + strJurisFullName);
                                                            //break;
                                                        }

                                                    }                                                    
                                                }
                                                
                                            }
                                            else // reader2 query returned with zero rows - must be a new segment for utrans
                                            {
                                                intUniqueID = intUniqueID + 1;
                                                streamWriter.WriteLine(intUniqueID + ",-1," + "0," + intJurisOID + "," + "NEW_MAYBE," + "QueryReturnedNoRecords" + "," + ",");
                                            }
                                        }
                                    }
                                }
                            } // end of main while loop - that loops through the source jurisdiction's features

                            //close the stream writer
                            streamWriter.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error with the GrandCo_AggregateCityCountyRoads console application." + ex.Message);
                Console.ReadLine();
            }
            finally
            {
            }
        }



        // get direction of street segment
        static double getLineDirection (double dblEndY, double dblStartY, double dblEndX, double dblStartX)
        {
            try
            {
                var a = Math.Atan2(dblEndY - dblStartY, dblEndX - dblStartX);
                if (a < 0) a += 2 * Math.PI; //angle is now in radians

                a -= (Math.PI / 2); //shift by 90deg
                //restore value in range 0-2pi instead of -pi/2-3pi/2
                if (a < 0) a += 2 * Math.PI;
                if (a < 0) a += 2 * Math.PI;
                a = Math.Abs((Math.PI * 2) - a); //invert rotation
                a = a * 180 / Math.PI; //convert to deg

                return a;

            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error with the getLineDirection method in the GrandCo_AggregateCityCountyRoads console application." + ex.Message);
                Console.ReadLine();
                return -1;
            }
        }



        // parse out the sql datareader's point (well known text)
        static double getX_fromPnt(string strPntWKT)
        {
            try
            {
                strPntWKT = strPntWKT.Replace("(", String.Empty);
                strPntWKT = strPntWKT.Replace(")", String.Empty);

                string[] strParsed = strPntWKT.Split(' ');

                string strGetX = strParsed[1].Trim();
                //string strGetY = strParsed[2].Trim();

                double dblGetX = Convert.ToDouble(strGetX);
                //double dblGetY = Convert.ToDouble(strGetY);

                return dblGetX;
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error with the getX_fromPnt method in the GrandCo_AggregateCityCountyRoads console application." + ex.Message);
                Console.ReadLine();
                return -1;
            }
        }



        // parse out the sql datareader's point (well known text)
        static double getY_fromPnt(string strPntWKT)
        {
            try
            {
                strPntWKT = strPntWKT.Replace("(", String.Empty);
                strPntWKT = strPntWKT.Replace(")", String.Empty);

                string[] strParsed = strPntWKT.Split(' ');

                //string strGetX = strParsed[1].Trim();
                string strGetY = strParsed[2].Trim();

                //double dblGetX = Convert.ToDouble(strGetX);
                double dblGetY = Convert.ToDouble(strGetY);

                return dblGetY;
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error with the getY_fromPnt method in the GrandCo_AggregateCityCountyRoads console application." + ex.Message);
                Console.ReadLine();
                return -1;
            }
        }




    }
}

