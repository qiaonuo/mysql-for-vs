// Copyright (C) 2004-2006 MySQL AB
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License version 2 as published by
// the Free Software Foundation
//
// There are special exceptions to the terms and conditions of the GPL 
// as it is applied to this software. View the full text of the 
// exception in file EXCEPTIONS in the directory of this software 
// distribution.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.Security.Cryptography;
using MySql.Data.Common;
using System.Collections;
using System.Text;
using MySql.Data.Types;

namespace MySql.Data.MySqlClient
{
	/// <summary>
	/// Summary description for Driver.
	/// </summary>
	internal class NativeDriver : Driver
	{
		public    int					MaxSinglePacket = 255 * 255 * 255;
		protected byte					packetSeq;

		protected int					protocol;
		protected String				encryptionSeed;
		protected ClientFlags			connectionFlags;

		protected MySqlStreamReader		reader;
		protected MySqlStreamWriter		writer;
		private   BitArray				nullMap;

        private int warningCount;

		public NativeDriver(MySqlConnectionStringBuilder settings) : base(settings)
		{
			packetSeq = 0;
			isOpen = false;
			maxPacketSize = 1047552;
		}

		public ClientFlags Flags
		{
			get { return connectionFlags; }
		}

		/// <summary>
		/// Returns true if this connection can handle batch SQL natively
		/// This means MySQL 4.1.1 or later.
		/// </summary>
		public override bool SupportsBatch 
		{
			get 
			{ 
				if ((Flags & ClientFlags.MULTI_STATEMENTS) != 0)
				{
					if (version.isAtLeast(4, 1, 0) && ! version.isAtLeast(4, 1, 10))
					{
						if (serverProps["query_cache_type"].Equals("ON") &&
							!serverProps["query_cache_size"].Equals("0")) return false;
					}
					return true;
				}
				return false;
			}
		}


        /// <summary>
        /// ExecuteCommand does the work of writing the actual command bytes to the writer
        /// We break it out into a function since it is used in several places besides query
        /// </summary>
        /// <param name="cmd">The cmd that we are sending</param>
        /// <param name="bytes">The bytes of the command, can be null</param>
        /// <param name="length">The number of bytes to send</param>
		private void ExecuteCommand(DBCmd cmd, byte[] bytes, int length) 
		{
			int len = 1;
			if (bytes != null)
				len += length;
			writer.StartPacket(len, true);
			writer.WriteByte((byte)cmd);
			if (bytes != null)
				writer.Write(bytes, 0, length);
			writer.Flush();
		}

		private void ReadOk(bool read) 
		{
			if (read)
				reader.OpenPacket();
			byte marker = reader.ReadByte();
			if (marker != 0)
				throw new MySqlException("Out of sync with server", true, null);

			long affectedRows = reader.GetFieldLength();
			long lastInsertId = reader.GetFieldLength();
			if (reader.HasMoreData)
			{
				serverStatus = (ServerStatusFlags)reader.ReadInteger(2);
				int warningCount = reader.ReadInteger(2);
				if (reader.HasMoreData)
				{
					string msg = reader.ReadLenString();
				}
			}
		}

		/// <summary>
		/// Sets the current database for the this connection
		/// </summary>
		/// <param name="dbName"></param>
		public override void SetDatabase(string dbName)
		{
			byte[] dbNameBytes = Encoding.GetBytes( dbName );
			ExecuteCommand( DBCmd.INIT_DB, dbNameBytes, dbNameBytes.Length );
			ReadOk(true);
		}

		public string GetString(byte[] stringBuffer)
		{
			if (stringBuffer == null) return String.Empty;
			return encoding.GetString(stringBuffer, 0, stringBuffer.Length);
		}

		public byte[] GetBytes(string s)
		{
			return encoding.GetBytes(s);
		}

		public override void Open()
		{
			base.Open();

			// connect to one of our specified hosts
			Stream stream;
			try 
			{
#if !CF
				if (Settings.ConnectionProtocol == MySqlConnectionProtocol.SharedMemory)
				{
					SharedMemoryStream str = new SharedMemoryStream(Settings.SharedMemoryName);
					str.Open(Settings.ConnectionTimeout);
					stream = str;
				}
				else 
				{
#endif
					string pipeName = Settings.PipeName;
					if (Settings.ConnectionProtocol != MySqlConnectionProtocol.NamedPipe)
						pipeName = null;
					StreamCreator sc = new StreamCreator(Settings.Server, Settings.Port, pipeName);
					stream = sc.GetStream(Settings.ConnectionTimeout);
#if !CF
				}
#endif
			}
			catch (Exception ex)
			{
				throw new MySqlException("Unable to connect to any of the specified MySQL hosts", ex);
			}


			if (stream == null) 
				throw new MySqlException("Unable to connect to any of the specified MySQL hosts");
			MySqlStream myStream = new MySqlStream(stream);

			reader = new MySqlStreamReader(myStream, encoding);
			writer = new MySqlStreamWriter(myStream, encoding);

			// read off the welcome packet and parse out it's values
			reader.OpenPacket();
			protocol = reader.ReadByte();
			string versionString = reader.ReadString();
			version = DBVersion.Parse( versionString );
			threadId = (int)reader.ReadInteger(4);
			encryptionSeed = reader.ReadString();

			// starting with 4.0.8, maxSinglePacket should be 0xffffff
			if ( version.isAtLeast(4,0,8))
				MaxSinglePacket = (256*256*256)-1;

			// read in Server capabilities if they are provided
			serverCaps = 0;
			if (reader.HasMoreData)
				serverCaps = (ClientFlags)reader.ReadInteger(2);

			// based on our settings, set our connection flags
			SetConnectionFlags();

			writer.StartPacket(0, false);
			writer.WriteInteger( (int)connectionFlags, version.isAtLeast(4,1,0) ? 4 : 2 );
			writer.WriteInteger( MaxSinglePacket, version.isAtLeast(4,1,0) ? 4 : 3 );

			// 4.1.1 included some new server status info
			if ( version.isAtLeast(4,1,1))
			{
				/* New protocol with 16 bytes to describe server characteristics */
				serverCharSetIndex = reader.ReadInteger(1);

				serverStatus = (ServerStatusFlags)reader.ReadInteger(2);
				reader.SkipBytes( 13 );

				string seedPart2 = reader.ReadString();
				encryptionSeed += seedPart2;

				writer.WriteByte( 8 );
				writer.Write( new byte[23], 0, 23 );
			}

			Authenticate();

			// if we are using compression, then we use our CompressedStream class
			// to hide the ugliness of managing the compression
			if ((connectionFlags & ClientFlags.COMPRESS) != 0)
			{
				MySqlStream compStream = new MySqlStream(new CompressedStream(stream));
				reader = new MySqlStreamReader(compStream, encoding);
				writer = new MySqlStreamWriter(compStream, encoding);
			}

			((MySqlStream)reader.Stream).MaxSinglePacket = MaxSinglePacket;
			((MySqlStream)writer.Stream).MaxSinglePacket = MaxSinglePacket;

			// give our reader the server version we are connected to.  Our reader may have some fields
			// that are read differently based on the version of the server we are connected to.
			reader.Version = writer.Version = this.version;

			isOpen = true;
		}

		/// <summary>
		/// Return the appropriate set of connection flags for our
		/// server capabilities and our user requested options.
		/// </summary>
		private void SetConnectionFlags()
		{
			ClientFlags flags = ClientFlags.FOUND_ROWS;

			if ( version.isAtLeast(4,1,1) )
			{
				flags |= ClientFlags.PROTOCOL_41;
				// Need this to get server status values
				flags |= ClientFlags.TRANSACTIONS;

				// user allows/disallows batch statements
				if (connectionString.AllowBatch)
					flags |= ClientFlags.MULTI_STATEMENTS;

				// We always allow multiple result sets
				flags |= ClientFlags.MULTI_RESULTS;
			}
			else if ( version.isAtLeast( 4, 1, 0 ) )
				flags |= ClientFlags.RESERVED;
			
			// if the server allows it, tell it that we want long column info
			if ((serverCaps & ClientFlags.LONG_FLAG) != 0)
				flags |= ClientFlags.LONG_FLAG;

			// if the server supports it and it was requested, then turn on compression
			if ((serverCaps & ClientFlags.COMPRESS) != 0 && connectionString.UseCompression)
				flags |= ClientFlags.COMPRESS;

			if ( protocol > 9 )
				flags |= ClientFlags.LONG_PASSWORD; // for long passwords
			else 
				flags &= ~ClientFlags.LONG_PASSWORD;

			// allow load data local infile
			flags |= ClientFlags.LOCAL_FILES;

			// if the server allows it and a database was specified, then indicate
			// that we will connect with a database name
			if ((serverCaps & ClientFlags.CONNECT_WITH_DB) != 0 && 
				connectionString.Database != null && connectionString.Database.Length > 0)
				flags |= ClientFlags.CONNECT_WITH_DB;

			// if the server is requesting a secure connection, then we oblige
			if ((serverCaps & ClientFlags.SECURE_CONNECTION) != 0)
				flags |= ClientFlags.SECURE_CONNECTION;

			connectionFlags = flags;
		}


		/// <summary>Perform an authentication against a 4.1.1 server</summary>
		private void Authenticate411()
		{
			if ( (connectionFlags & ClientFlags.SECURE_CONNECTION) == 0)
				AuthenticateOld();

			writer.Write( Crypt.Get411Password( connectionString.Password, this.encryptionSeed ));
			if ( (connectionFlags & ClientFlags.CONNECT_WITH_DB) != 0 && connectionString.Database != null)
				writer.WriteString( connectionString.Database );
			writer.Flush();

			// this result means the server wants us to send the password using
			// old encryption
			reader.OpenPacket();
			if (reader.IsLastPacket)
			{
				writer.StartPacket(0, false);
				writer.WriteString( Crypt.EncryptPassword( 
					connectionString.Password, this.encryptionSeed.Substring(0,8), true ) );
				writer.Flush();
				ReadOk(true);
			}
			else 
				ReadOk(false);
		}

		private void AuthenticateOld()
		{
			writer.WriteString( Crypt.EncryptPassword( 
				connectionString.Password, encryptionSeed, protocol > 9));
			if ( (connectionFlags & ClientFlags.CONNECT_WITH_DB) != 0 && connectionString.Database != null)
				writer.WriteString( connectionString.Database );
			writer.Flush();
			ReadOk(true);
		}

		public void Authenticate()
		{
			// write the user id to the auth packet
			writer.WriteString( connectionString.UserID ); 

			if ( version.isAtLeast(4,1,1) )
				Authenticate411();
			else
				AuthenticateOld();
		}

		public override void Reset()
		{
			writer.StartPacket(0, true);
			writer.WriteByte((byte)DBCmd.CHANGE_USER);
			Authenticate();
		}

        /// <summary>
        /// Query is the method that is called to send all queries to the server
        /// </summary>
        /// <param name="bytes">The query to send</param>
        /// <param name="length">The length of the query to send</param>
        /// <returns>
        /// -1 for non select queries
        /// >= 0 for select queries
        /// </returns>
		public override void Query(byte[] bytes, int length)
		{
			if (Settings.Logging)
				Logger.LogCommand(DBCmd.QUERY, encoding.GetString(bytes, 0, length));

            // send the command to the server
			ExecuteCommand(DBCmd.QUERY, bytes, length);

            // the server will respond in one of several ways with the first byte indicating
            // the type of response.
            // 0 == ok packet.  This indicates non-select queries
            // 0xff == error packet.  This is handled in reader.OpenPacket
            // > 0 = number of columns in select query
            // We don't actually read the result here since a single query can generate
            // multiple resultsets and we don't want to duplicate code.  See ReadResult
            // Instead we set our internal server status flag to indicate that we have a query waiting.
            // This flag will be maintained by ReadResult
			serverStatus |= ServerStatusFlags.AnotherQuery;
		}

		public override void Close() 
		{
			if (isOpen)
				ExecuteCommand( DBCmd.QUIT, null, 0 );

			writer.Stream.Close();
			reader.Close();

			base.Close();
		}


		public override bool Ping() 
		{
			try 
			{
				ExecuteCommand(DBCmd.PING, null, 0); 
				ReadOk(true);
				return true;
			}
			catch (Exception) 
			{
				isOpen = false;
				// exceptions here can be very common so we don't log
				return false;
			}
		}

        /// <summary>
        /// ReadResult will attempt to read a single result from the server.  Note that it is not 
        /// reading all the rows of the result set but simple determining what type of result it is
        /// and returning values appropriately.
        /// </summary>
        /// <param name="affectedRows">Set to the number of rows affected in this result, 0 for selects</param>
        /// <param name="lastInsertId">Set to the id of the row inserted by this result, 0 for non-inserts</param>
        /// <returns>Number of columns in the resultset, 0 for non-selects, -1 for no more resultsets</returns>
		public override long ReadResult(ref ulong affectedRows, ref long lastInsertId)
		{
            // if there is not another query or resultset, then return -1
            if ((serverStatus & (ServerStatusFlags.AnotherQuery | ServerStatusFlags.MoreResults)) == 0)
                return -1;

			reader.OpenPacket();

			long fieldCount = reader.GetFieldLength();
            if (fieldCount > 0)
                return fieldCount;

			if (-1 == fieldCount)
			{
				string filename = reader.ReadString();
				SendFileToServer(filename);

                return ReadResult(ref affectedRows, ref lastInsertId);
			}

			affectedRows = (ulong)reader.GetFieldLength();
			lastInsertId = (long)reader.GetFieldLength();
			if ( version.isAtLeast(4,1,0) ) 
			{
				serverStatus = (ServerStatusFlags)reader.ReadInteger(2);
				warningCount = reader.ReadInteger(2);
				if (reader.HasMoreData) 
				{
					reader.ReadLenString();  //TODO: server message
				}
			}
            return fieldCount;
		}

		/// <summary>
		/// Sends the specified file to the server. 
		/// This supports the LOAD DATA LOCAL INFILE
		/// </summary>
		/// <param name="filename"></param>
		private void SendFileToServer( string filename )
		{
			byte[]		buffer = new byte[4092];
			FileStream	fs = null;


			try 
			{
				fs = new FileStream(filename, FileMode.Open);
				writer.StartPacket(fs.Length, true);

				long len = fs.Length;
				while (len > 0) 
				{
					int count = fs.Read( buffer, 0, 4092 );
					writer.Write( buffer, 0, count );
					len -= count;
				}
				writer.Flush();

				// write the terminating packet
				//TODO: fix this
//				writer.WriteInteger(0, 3);
//				writer.WriteByte(this.SequenceByte ++);
//				writer.Flush();
			}
			catch (Exception ex)
			{
				throw new MySqlException("Error during LOAD DATA LOCAL INFILE", ex);
			}
			finally 
			{
				fs.Close();
			}
		}

        public override bool SkipDataRow()
        {
            bool result = true;
            if (!reader.HasMoreData)
                result = FetchDataRow(-1, 0, 0);
            if (result)
                reader.SkipPacket();
            return result;
        }

		private void ReadNullMap( int fieldCount ) 
		{
			// if we are binary, then we need to load in our null bitmap
			nullMap = null;
			byte[] nullMapBytes = new byte[ (fieldCount+9)/8 ];
			reader.ReadByte();
			reader.Read(nullMapBytes, 0, nullMapBytes.Length);
			nullMap = new BitArray( nullMapBytes );
		}

		public override IMySqlValue ReadColumnValue(int index, MySqlField field, IMySqlValue valObject) 
		{
			long length = -1;
			bool isNull = false;

			if (nullMap != null) 
				isNull = nullMap [index+2];
			else
			{
				length = reader.GetFieldLength();
				isNull = length == -1;
			}

//			if (valObject.IsNull) return valObject;

			reader.Encoding = field.Encoding;
			return valObject.ReadValue(reader, length, isNull);
		}

		public override void SkipColumnValue(IMySqlValue valObject)
		{
			long length = -1;
			if (nullMap == null)
			{
				length = reader.GetFieldLength();
				if (length == -1) return;
			}
			if (length > -1)
				reader.SkipBytes( (int)length);
			else
				valObject.SkipValue(reader);				
		}

		public override MySqlField[] ReadColumnMetadata(int count)
		{
			MySqlField[] fields = new MySqlField[count];

			for (int i=0; i < count; i++)
				fields[i] = GetFieldMetaData();

			ReadEOF();
            return fields;
		}


		private MySqlField GetFieldMetaData() 
		{
			MySqlField field = null;

			if ( version.isAtLeast(4,1,0) )
				field = GetFieldMetaData41();
			else 
			{
				reader.OpenPacket();
				field = new MySqlField( this.Version );

				field.Encoding = encoding;
				field.TableName = reader.ReadLenString();
				field.ColumnName = reader.ReadLenString();
				field.ColumnLength = reader.ReadNBytes();
				MySqlDbType type = (MySqlDbType)reader.ReadNBytes();
				reader.ReadByte();
				if ((Flags & ClientFlags.LONG_FLAG) != 0)
					field.Flags = (ColumnFlags)reader.ReadInteger(2);
				else 
					field.Flags = (ColumnFlags)reader.ReadByte();

				// we delay this because setting the type causes the internal type object to be created
				field.Type = type;

				field.Scale = (byte)reader.ReadByte();
				if ( !version.isAtLeast(3,23,15) && version.isAtLeast(3,23,0))
					field.Scale++;
			}

			return field;
		}

		private MySqlField GetFieldMetaData41() 
		{
			MySqlField field = new MySqlField( this.Version );

			reader.OpenPacket();
			field.Encoding = encoding;
			field.CatalogName = reader.ReadLenString();
			field.DatabaseName = reader.ReadLenString();
			field.TableName = reader.ReadLenString();
			field.RealTableName = reader.ReadLenString();
			field.ColumnName = reader.ReadLenString();
			field.OriginalColumnName = reader.ReadLenString();
			reader.ReadByte();
			field.CharactetSetIndex = reader.ReadInteger(2);
			field.ColumnLength = reader.ReadInteger(4);
			field.Type = (MySqlDbType)reader.ReadByte();
			if ((Flags & ClientFlags.LONG_FLAG) != 0)
				field.Flags = (ColumnFlags)reader.ReadInteger(2);
			else 
				field.Flags = (ColumnFlags)reader.ReadByte();

			field.Scale = (byte)reader.ReadByte();

			if (reader.HasMoreData)
				reader.ReadInteger(2);	// reserved

			if (charSets != null)
				field.Encoding = CharSetMap.GetEncoding( this.version, (string)charSets[field.CharactetSetIndex] );

			return field;
		}


		public override void ExecuteStatement(byte[] bytes)
		{
			ExecuteCommand(DBCmd.EXECUTE, bytes, bytes.Length);
            serverStatus |= ServerStatusFlags.AnotherQuery;
        }

        private void CheckEOF()
        {
            if (!reader.IsLastPacket)
                throw new MySqlException("Expected end of data packet");

            reader.ReadByte();  // read off the 254

            if (reader.HasMoreData && version.isAtLeast(4, 1, 0))
            {
                warningCount = reader.ReadInteger(2);
                serverStatus = (ServerStatusFlags)reader.ReadInteger(2);

                // if we are at the end of this cursor based resultset, then we remove
                // the last row sent status flag so our next fetch doesn't abort early
                // and we remove this command result from our list of active CommandResult objects.
//                if ((serverStatus & ServerStatusFlags.LastRowSent) != 0)
  //              {
    //                serverStatus &= ~ServerStatusFlags.LastRowSent;
      //              commandResults.Remove(lastCommandResult);
        //        }
            }
        }

        private void ReadEOF() 
		{
			reader.OpenPacket();
            CheckEOF();
		}

		public override int PrepareStatement(string sql, ref MySqlField[] parameters) 
		{
            //TODO: check this
			//ClearFetchedRow();

			byte[] bytes = encoding.GetBytes(sql);
			ExecuteCommand(DBCmd.PREPARE, bytes, bytes.Length);

			reader.OpenPacket();

			int marker = reader.ReadByte();
			if (marker != 0)
				throw new MySqlException("Expected prepared statement marker");

			int statementId = reader.ReadInteger(4);
			int numCols = reader.ReadInteger(2);
			int numParams = reader.ReadInteger(2);

			if (numParams > 0)
			{
				parameters = ReadColumnMetadata(numParams);

				// we set the encoding for each parameter back to our connection encoding
				// since we can't trust what is coming back from the server
				for (int i=0; i < parameters.Length; i++)
					parameters[i].Encoding = encoding;
			}

			if (numCols > 0) 
			{
                while (numCols-- > 0)
                {
                    reader.OpenPacket();
                    reader.SkipPacket();
                }

				ReadEOF();
			}

			return statementId;
		}

//		private void ClearFetchedRow() 
//		{
//			if (lastCommandResult == 0) return;

            //TODO
/*			CommandResult result = (CommandResult)commandResults[lastCommandResult];
			result.ReadRemainingColumns();

			reader.OpenPacket();
			if (! reader.IsLastPacket)
				throw new MySqlException("Cursor reading out of sync");

			ReadEOF(false);
			lastCommandResult = 0;*/
//		}

        /// <summary>
        /// FetchDataRow is the method that the data reader calls to see if there is another 
        /// row to fetch.  In the non-prepared mode, it will simply read the next data packet.
        /// In the prepared mode (statementId > 0), it will 
        /// </summary>
		public override bool FetchDataRow(int statementId, int pageSize, int columns)
		{
/*			ClearFetchedRow();

			if (!commandResults.ContainsKey(statementId)) return false;

			if ( (serverStatus & ServerStatusFlags.LastRowSent) != 0)
				return false;

			writer.StartPacket(9, true);
			writer.WriteByte((byte)DBCmd.FETCH);
			writer.WriteInteger(statementId, 4);
			writer.WriteInteger(1, 4);
			writer.Flush();

			lastCommandResult = statementId;
            */
			reader.OpenPacket();
			if (reader.IsLastPacket)
			{
				CheckEOF();
				return false;
			}
            nullMap = null;
            if (statementId > 0)
                ReadNullMap(columns);

			return true;
		}

    }
}
