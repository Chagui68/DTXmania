using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FDK;

namespace DTXMania
{
	internal static class CChartMidiToDtx
	{
		private const int DTX_LEAD_TICKS = 384;
		private const int TICKS_PER_BEAT = 96;
		private const int TICKS_PER_MEASURE = 384;
		private const int SLOTS_PER_MEASURE = 384;
		private const string SAMPLE_PREFIX = "_dtxmania_";
		private const int BGM_WAV_NUMBER = 1;

		private static readonly EChannel[] arLaneChannel = new EChannel[]
		{
			EChannel.BassDrum,
			EChannel.Snare,
			EChannel.HiHatClose,
			EChannel.HighTom,
			EChannel.Cymbal,
		};
		private static readonly string[] arLaneSample = new string[] { "kick", "snare", "hat", "tom", "cymbal" };
		private static readonly int[] arLaneWavNumber = new int[] { 2, 3, 4, 5, 6 };

		private sealed class CNote
		{
			public long nTick;
			public int nLane;
		}

		private sealed class CTempo
		{
			public long nTick;
			public double dbBpm;
		}

		private sealed class CScoreData
		{
			public bool bIsMidi;
			public string strTitle = "";
			public string strArtist = "";
			public string strGenre = "";
			public string strMusicFile;
			public int nResolution = 192;
			public int nDrumLevel;
			public List<CNote> listNote = new List<CNote>();
			public List<CTempo> listTempo = new List<CTempo>();
		}

		private struct CChipData
		{
			public EChannel channel;
			public long nPosition;
			public int nObject;
		}

		public static string tConvert( string strFilePath, bool bHeaderOnly )
		{
			string strFolder = Path.GetDirectoryName( Path.GetFullPath( strFilePath ) );
			string ext = Path.GetExtension( strFilePath ).ToLower();
			CScoreData data;
			if( ext == ".chart" )
			{
				data = tParseChart( strFilePath, strFolder );
			}
			else
			{
				data = tParseMidi( strFilePath, strFolder );
			}
			return tBuildDtx( data, strFolder, bHeaderOnly );
		}

		#region [ .chart ]
		private static CScoreData tParseChart( string strFilePath, string strFolder )
		{
			CScoreData data = new CScoreData();
			data.bIsMidi = false;
			if( !File.Exists( strFilePath ) )
			{
				return data;
			}

			Dictionary<string, string> dicSong =
				new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			List<KeyValuePair<long, int>> listDrumNote = new List<KeyValuePair<long, int>>();

			string strSection = "";
			string strDrumsSection = "";
			string[] arPriority = new string[] { "ExpertDrums", "HardDrums", "MediumDrums", "EasyDrums", "Drums" };

			string[] arLines = File.ReadAllLines( strFilePath, Encoding.UTF8 );
			for( int i = 0; i < arLines.Length; i++ )
			{
				string strLine = arLines[ i ].Trim();
				if( strLine.Length == 0 || strLine.StartsWith( "//" ) )
				{
					continue;
				}
				if( strLine.StartsWith( "[" ) )
				{
					int nEnd = strLine.IndexOf( ']' );
					strSection = ( nEnd > 1 ) ? strLine.Substring( 1, nEnd - 1 ) : "";
					if( Array.IndexOf( arPriority, strSection ) >= 0 )
					{
						if( strDrumsSection.Length == 0 ||
							Array.IndexOf( arPriority, strSection ) < Array.IndexOf( arPriority, strDrumsSection ) )
						{
							strDrumsSection = strSection;
							listDrumNote.Clear();
						}
					}
					continue;
				}

				if( strSection.Equals( "Song", StringComparison.OrdinalIgnoreCase ) )
				{
					int nEq = strLine.IndexOf( '=' );
					if( nEq > 0 )
					{
						string strKey = strLine.Substring( 0, nEq ).Trim();
						string strValue = strLine.Substring( nEq + 1 ).Trim().Trim( '"' );
						dicSong[ strKey ] = strValue;
					}
				}
				else if( strSection.Equals( "SyncTrack", StringComparison.OrdinalIgnoreCase ) )
				{
					long nTick;
					if( tParseChartTick( strLine, out nTick ) )
					{
						if( strLine.IndexOf( " B ", StringComparison.Ordinal ) > 0 ||
							strLine.IndexOf( "= B ", StringComparison.Ordinal ) > 0 )
						{
							int nPos = strLine.IndexOf( "B ", StringComparison.Ordinal );
							double dbValue;
							if( double.TryParse( strLine.Substring( nPos + 2 ).Trim().Split( ' ' )[ 0 ],
								NumberStyles.Float, CultureInfo.InvariantCulture, out dbValue ) )
							{
								data.listTempo.Add( new CTempo() { nTick = nTick, dbBpm = dbValue / 1000.0 } );
							}
						}
					}
				}
				else if( strSection.Length > 0 && strSection.Equals( strDrumsSection, StringComparison.OrdinalIgnoreCase ) )
				{
					long nTick;
					if( tParseChartTick( strLine, out nTick ) )
					{
						int nNote = tParseChartNoteValue( strLine );
						if( nNote >= 0 && nNote <= 4 )
						{
							listDrumNote.Add( new KeyValuePair<long, int>( nTick, nNote ) );
						}
					}
				}
			}

			string strRes;
			if( dicSong.TryGetValue( "Resolution", out strRes ) )
			{
				int nRes;
				if( int.TryParse( strRes, out nRes ) && nRes > 0 )
				{
					data.nResolution = nRes;
				}
			}

			string strValue2;
			if( dicSong.TryGetValue( "Name", out strValue2 ) )
			{
				data.strTitle = strValue2;
			}
			if( dicSong.TryGetValue( "Artist", out strValue2 ) )
			{
				data.strArtist = strValue2;
			}
			if( dicSong.TryGetValue( "Genre", out strValue2 ) )
			{
				data.strGenre = strValue2;
			}
			if( dicSong.TryGetValue( "MusicStream", out strValue2 ) )
			{
				data.strMusicFile = strValue2;
			}

			foreach( KeyValuePair<long, int> kv in listDrumNote )
			{
				data.listNote.Add( new CNote() { nTick = kv.Key, nLane = kv.Value } );
			}

			tApplySongIni( data, strFolder );
			tResolveMusicFile( data, strFolder );
			tEstimateDrumLevel( data );
			return data;
		}

		private static bool tParseChartTick( string strLine, out long nTick )
		{
			nTick = 0;
			int nEq = strLine.IndexOf( '=' );
			if( nEq <= 0 )
			{
				return false;
			}
			return long.TryParse( strLine.Substring( 0, nEq ).Trim(), out nTick );
		}

		private static int tParseChartNoteValue( string strLine )
		{
			int nEq = strLine.IndexOf( '=' );
			if( nEq <= 0 )
			{
				return -1;
			}
			string[] arToken = strLine.Substring( nEq + 1 ).Trim().Split( ' ' );
			if( arToken.Length < 2 || !arToken[ 0 ].Equals( "N", StringComparison.OrdinalIgnoreCase ) )
			{
				return -1;
			}
			int nValue;
			if( !int.TryParse( arToken[ 1 ], out nValue ) )
			{
				return -1;
			}
			return nValue;
		}
		#endregion

		#region [ .mid ]
		private static CScoreData tParseMidi( string strFilePath, string strFolder )
		{
			CScoreData data = new CScoreData();
			data.bIsMidi = true;
			if( !File.Exists( strFilePath ) )
			{
				return data;
			}

			byte[] b = File.ReadAllBytes( strFilePath );
			if( b.Length < 0xE || b[ 0 ] != 'M' || b[ 1 ] != 'T' || b[ 2 ] != 'h' || b[ 3 ] != 'd' )
			{
				return data;
			}

			int nHeaderLength = tBigEndian32( b, 4 );
			int nTracks = tBigEndian16( b, 10 );
			int nDivision = tBigEndian16( b, 12 );
			if( ( nDivision & 0x8000 ) == 0 && nDivision > 0 )
			{
				data.nResolution = nDivision;
			}

			string strConductorName = "";
			List<CNote> listBestNote = null;
			int nBestScore = -1;

			int nPos = 8 + nHeaderLength;
			for( int t = 0; t < nTracks && nPos + 8 <= b.Length; t++ )
			{
				if( b[ nPos ] != 'M' || b[ nPos + 1 ] != 'T' || b[ nPos + 2 ] != 'r' || b[ nPos + 3 ] != 'k' )
				{
					break;
				}
				int nChunkLength = tBigEndian32( b, nPos + 4 );
				int nStart = nPos + 8;
				int nEnd = Math.Min( nStart + nChunkLength, b.Length );

				string strTrackName = "";
				List<CNote> listTrackNote = new List<CNote>();
				int nP = nStart;
				long nTicks = 0;
				int nRunningStatus = 0;
				while( nP < nEnd )
				{
					nTicks += tReadVarLength( b, ref nP, nEnd );
					if( nP >= nEnd )
					{
						break;
					}
					int nStatus = b[ nP ];
					if( ( nStatus & 0x80 ) != 0 )
					{
						nRunningStatus = nStatus;
						nP++;
					}
					else
					{
						nStatus = nRunningStatus;
					}
					int nHigh = nStatus & 0xF0;

					if( nHigh == 0xF0 )
					{
						if( nStatus == 0xFF )
						{
							if( nP >= nEnd )
							{
								break;
							}
							int nType = b[ nP ];
							nP++;
							int nLength = tReadVarLength( b, ref nP, nEnd );
							if( nType == 0x03 )
							{
								strTrackName = tReadText( b, nP, nLength, nEnd );
							}
							else if( nType == 0x51 && nLength >= 3 )
							{
								double dbMicroSeconds = ( b[ nP ] << 16 ) | ( b[ nP + 1 ] << 8 ) | b[ nP + 2 ];
								if( dbMicroSeconds > 0.0 )
								{
									data.listTempo.Add( new CTempo()
									{
										nTick = nTicks,
										dbBpm = 60000000.0 / dbMicroSeconds,
									} );
								}
							}
							nP += nLength;
						}
						else
						{
							int nLength = tReadVarLength( b, ref nP, nEnd );
							nP += nLength;
						}
						continue;
					}

					int nData1 = b[ nP ];
					nP++;
					if( nHigh == 0x90 || nHigh == 0x80 )
					{
						int nData2 = b[ nP ];
						nP++;
						if( nHigh == 0x90 && nData2 > 0 )
						{
							listTrackNote.Add( new CNote() { nTick = nTicks, nLane = nData1 } );
						}
					}
					else
					{
						bool bTwoBytes = ( nHigh != 0xC0 && nHigh != 0xD0 );
						if( bTwoBytes )
						{
							nP++;
						}
					}
				}

				if( t == 0 )
				{
					strConductorName = strTrackName;
				}

				bool bDrumName = strTrackName.IndexOf( "Drum", StringComparison.OrdinalIgnoreCase ) >= 0;
				int nScore = ( bDrumName ? 1000000 : 0 ) + listTrackNote.Count;
				if( listTrackNote.Count > 0 && nScore > nBestScore )
				{
					nBestScore = nScore;
					listBestNote = listTrackNote;
				}

				nPos = nStart + nChunkLength;
			}

			int nBase = tFindDrumOctave( listBestNote );
			if( listBestNote != null )
			{
				foreach( CNote note in listBestNote )
				{
					int nLane = note.nLane - nBase;
					if( nLane >= 0 && nLane <= 4 )
					{
						data.listNote.Add( new CNote() { nTick = note.nTick, nLane = nLane } );
					}
				}
			}

			if( strConductorName.Length > 0 )
			{
				data.strTitle = strConductorName;
			}

			tApplySongIni( data, strFolder );
			tResolveMusicFile( data, strFolder );
			tEstimateDrumLevel( data );
			return data;
		}

		private static int tFindDrumOctave( List<CNote> listNote )
		{
			if( listNote == null )
			{
				return 96;
			}
			int[] arBase = new int[] { 96, 84, 72, 60 };
			foreach( int nBase in arBase )
			{
				foreach( CNote note in listNote )
				{
					if( note.nLane >= nBase && note.nLane <= ( nBase + 4 ) )
					{
						return nBase;
					}
				}
			}
			return 96;
		}

		private static int tReadVarLength( byte[] b, ref int nPos, int nEnd )
		{
			int nValue = 0;
			while( nPos < nEnd )
			{
				int nByte = b[ nPos ];
				nPos++;
				nValue = ( nValue << 7 ) | ( nByte & 0x7F );
				if( ( nByte & 0x80 ) == 0 )
				{
					break;
				}
			}
			return nValue;
		}

		private static string tReadText( byte[] b, int nPos, int nLength, int nEnd )
		{
			int nLen = Math.Min( nLength, nEnd - nPos );
			if( nLen <= 0 )
			{
				return "";
			}
			return Encoding.UTF8.GetString( b, nPos, nLen );
		}

		private static int tBigEndian16( byte[] b, int nPos )
		{
			return ( b[ nPos ] << 8 ) | b[ nPos + 1 ];
		}

		private static int tBigEndian32( byte[] b, int nPos )
		{
			return ( b[ nPos ] << 24 ) | ( b[ nPos + 1 ] << 16 ) | ( b[ nPos + 2 ] << 8 ) | b[ nPos + 3 ];
		}
		#endregion

		#region [ song.ini / 音楽ファイル ]
		private static void tApplySongIni( CScoreData data, string strFolder )
		{
			string strIni = Path.Combine( strFolder, "song.ini" );
			if( !File.Exists( strIni ) )
			{
				return;
			}
			try
			{
				foreach( string strRaw in File.ReadAllLines( strIni, Encoding.UTF8 ) )
				{
					string strLine = strRaw.Trim();
					if( strLine.Length == 0 || strLine.StartsWith( "[" ) )
					{
						continue;
					}
					int nEq = strLine.IndexOf( '=' );
					if( nEq <= 0 )
					{
						continue;
					}
					string strKey = strLine.Substring( 0, nEq ).Trim();
					string strValue = strLine.Substring( nEq + 1 ).Trim().Trim( '"' );
					if( strKey.Equals( "name", StringComparison.OrdinalIgnoreCase ) ||
						strKey.Equals( "song", StringComparison.OrdinalIgnoreCase ) )
					{
						data.strTitle = strValue;
					}
					else if( strKey.Equals( "artist", StringComparison.OrdinalIgnoreCase ) )
					{
						data.strArtist = strValue;
					}
					else if( strKey.Equals( "genre", StringComparison.OrdinalIgnoreCase ) )
					{
						data.strGenre = strValue;
					}
					else if( strKey.Equals( "diff_drums", StringComparison.OrdinalIgnoreCase ) )
					{
						int nLevel;
						if( int.TryParse( strValue, out nLevel ) )
						{
							data.nDrumLevel = nLevel;
						}
					}
				}
			}
			catch
			{
			}
		}

		private static void tResolveMusicFile( CScoreData data, string strFolder )
		{
			if( !string.IsNullOrEmpty( data.strMusicFile ) )
			{
				string strCandidate = Path.Combine( strFolder, data.strMusicFile );
				if( File.Exists( strCandidate ) )
				{
					return;
				}
			}
			string[] arCandidate = new string[] { "song.ogg", "song.mp3", "song.wav", "guitar.ogg", "music.ogg" };
			foreach( string strName in arCandidate )
			{
				if( File.Exists( Path.Combine( strFolder, strName ) ) )
				{
					data.strMusicFile = strName;
					return;
				}
			}
			data.strMusicFile = null;
		}

		private static void tEstimateDrumLevel( CScoreData data )
		{
			if( data.nDrumLevel <= 0 )
			{
				int nCount = data.listNote.Count;
				if( nCount < 500 )
				{
					data.nDrumLevel = 2;
				}
				else if( nCount < 1500 )
				{
					data.nDrumLevel = 4;
				}
				else if( nCount < 3000 )
				{
					data.nDrumLevel = 6;
				}
				else if( nCount < 5000 )
				{
					data.nDrumLevel = 8;
				}
				else
				{
					data.nDrumLevel = 9;
				}
			}
			data.nDrumLevel = Math.Min( Math.Max( data.nDrumLevel, 1 ), 10 );
		}
		#endregion

		#region [ DTX テキスト生成 ]
		private static string tBuildDtx( CScoreData data, string strFolder, bool bHeaderOnly )
		{
			StringBuilder sb = new StringBuilder();

			sb.Append( "#TITLE " ).AppendLine( tSanitize( data.strTitle ) );
			sb.Append( "#ARTIST " ).AppendLine( tSanitize( data.strArtist ) );
			sb.Append( "#GENRE " ).AppendLine( tSanitize( data.strGenre ) );
			sb.Append( "#COMMENT Chart/MIDI (" ).Append( data.bIsMidi ? "mid" : "chart" ).AppendLine( ")" );
			sb.Append( "#DLEVEL " ).AppendLine( data.nDrumLevel.ToString( CultureInfo.InvariantCulture ) );

			List<CTempo> listTempo = new List<CTempo>( data.listTempo );
			listTempo.Sort( delegate( CTempo a, CTempo b ) { return a.nTick.CompareTo( b.nTick ); } );

			double dbInitialBpm = 120.0;
			if( listTempo.Count > 0 )
			{
				dbInitialBpm = listTempo[ 0 ].dbBpm;
				foreach( CTempo tempo in listTempo )
				{
					if( tempo.nTick == 0L )
					{
						dbInitialBpm = tempo.dbBpm;
						break;
					}
				}
			}
			sb.Append( "#BPM00 " ).AppendLine(
				dbInitialBpm.ToString( "0.######", CultureInfo.InvariantCulture ) );

			List<KeyValuePair<long, int>> listBpmChip = new List<KeyValuePair<long, int>>();
			int nBpmIndex = 1;
			foreach( CTempo tempo in listTempo )
			{
				if( tempo.nTick <= 0L || tempo.dbBpm <= 0.0 )
				{
					continue;
				}
				sb.Append( "#BPM" ).Append( CConversion.strConvertNumberTo2DigitBase36String( nBpmIndex ) )
					.Append( ' ' )
					.AppendLine( tempo.dbBpm.ToString( "0.######", CultureInfo.InvariantCulture ) );
				listBpmChip.Add( new KeyValuePair<long, int>( tempo.nTick, nBpmIndex ) );
				nBpmIndex++;
				if( nBpmIndex >= 36 * 36 )
				{
					break;
				}
			}

			if( !string.IsNullOrEmpty( data.strMusicFile ) )
			{
				sb.Append( "#WAV" ).Append( CConversion.strConvertNumberTo2DigitBase36String( BGM_WAV_NUMBER ) )
					.Append( ' ' ).AppendLine( tSanitize( data.strMusicFile ) );
			}

			if( !bHeaderOnly )
			{
				tEnsureSampleSounds( strFolder );
			}
			for( int i = 0; i < arLaneSample.Length; i++ )
			{
				sb.Append( "#WAV" ).Append( CConversion.strConvertNumberTo2DigitBase36String( arLaneWavNumber[ i ] ) )
					.Append( ' ' ).Append( SAMPLE_PREFIX ).Append( arLaneSample[ i ] ).AppendLine( ".wav" );
			}

			List<CChipData> listChip = new List<CChipData>();
			HashSet<string> setChip = new HashSet<string>();

			foreach( KeyValuePair<long, int> kv in listBpmChip )
			{
				long nPos = tConvertToDtxPosition( kv.Key, data.nResolution );
				tAddChip( listChip, setChip, EChannel.BPMEx, nPos, kv.Value );
			}

			HashSet<long> setUsedNote = new HashSet<long>();
			foreach( CNote note in data.listNote )
			{
				long nPos = tConvertToDtxPosition( note.nTick, data.nResolution );
				long nKey = ( nPos << 8 ) | (uint)note.nLane;
				if( setUsedNote.Contains( nKey ) )
				{
					continue;
				}
				setUsedNote.Add( nKey );
				tAddChip( listChip, setChip, arLaneChannel[ note.nLane ], nPos, arLaneWavNumber[ note.nLane ] );
			}

			if( !string.IsNullOrEmpty( data.strMusicFile ) )
			{
				tAddChip( listChip, setChip, EChannel.BGM, DTX_LEAD_TICKS, BGM_WAV_NUMBER );
			}

			tAppendChipLines( sb, listChip );
			return sb.ToString();
		}

		private static long tConvertToDtxPosition( long nTick, int nResolution )
		{
			if( nResolution <= 0 )
			{
				nResolution = 192;
			}
			double dbDtxTick = ( (double)nTick * TICKS_PER_BEAT ) / nResolution;
			return (long)Math.Round( dbDtxTick, MidpointRounding.AwayFromZero ) + DTX_LEAD_TICKS;
		}

		private static void tAddChip( List<CChipData> listChip, HashSet<string> setChip,
			EChannel channel, long nDtxPosition, int nObject )
		{
			string strKey = ( (int)channel ).ToString() + "@" + nDtxPosition.ToString();
			if( setChip.Add( strKey ) )
			{
				listChip.Add( new CChipData()
				{
					channel = channel,
					nPosition = nDtxPosition,
					nObject = nObject,
				} );
			}
		}

		private static void tAppendChipLines( StringBuilder sb, List<CChipData> listChip )
		{
			Dictionary<EChannel, SortedDictionary<int, SortedDictionary<int, int>>> dicByChannel =
				new Dictionary<EChannel, SortedDictionary<int, SortedDictionary<int, int>>>();
			foreach( CChipData chip in listChip )
			{
				int nMeasure = (int)( chip.nPosition / TICKS_PER_MEASURE ) - 1;
				int nSlot = (int)( chip.nPosition % TICKS_PER_MEASURE );
				if( nMeasure < 0 )
				{
					nMeasure = 0;
					nSlot = 0;
				}
				if( nMeasure >= 3600 )
				{
					continue;
				}
				SortedDictionary<int, SortedDictionary<int, int>> dicMeasure;
				if( !dicByChannel.TryGetValue( chip.channel, out dicMeasure ) )
				{
					dicMeasure = new SortedDictionary<int, SortedDictionary<int, int>>();
					dicByChannel[ chip.channel ] = dicMeasure;
				}
				SortedDictionary<int, int> dicSlot;
				if( !dicMeasure.TryGetValue( nMeasure, out dicSlot ) )
				{
					dicSlot = new SortedDictionary<int, int>();
					dicMeasure[ nMeasure ] = dicSlot;
				}
				if( !dicSlot.ContainsKey( nSlot ) )
				{
					dicSlot[ nSlot ] = chip.nObject;
				}
			}

			foreach( KeyValuePair<EChannel, SortedDictionary<int, SortedDictionary<int, int>>> kv in dicByChannel )
			{
				int nChannel = (int)kv.Key;
				foreach( KeyValuePair<int, SortedDictionary<int, int>> measure in kv.Value )
				{
					char[] arParam = new char[ SLOTS_PER_MEASURE * 2 ];
					for( int i = 0; i < arParam.Length; i++ )
					{
						arParam[ i ] = '0';
					}
					foreach( KeyValuePair<int, int> slot in measure.Value )
					{
						string strObject = CConversion.strConvertNumberTo2DigitBase36String( slot.Value );
						arParam[ slot.Key * 2 ] = strObject[ 0 ];
						arParam[ slot.Key * 2 + 1 ] = strObject[ 1 ];
					}
					sb.Append( '#' )
						.Append( CConversion.strConvertNumberTo3DigitMeasureNumber( measure.Key ) )
						.Append( nChannel.ToString( "X2" ) )
						.Append( ": " )
						.Append( arParam )
						.AppendLine();
				}
			}
		}
		#endregion

		#region [ サンプル音源の生成 ]
		private static void tEnsureSampleSounds( string strFolder )
		{
			if( string.IsNullOrEmpty( strFolder ) || !Directory.Exists( strFolder ) )
			{
				return;
			}
			for( int i = 0; i < arLaneSample.Length; i++ )
			{
				string strPath = Path.Combine( strFolder, SAMPLE_PREFIX + arLaneSample[ i ] + ".wav" );
				if( File.Exists( strPath ) )
				{
					continue;
				}
				try
				{
					byte[] bData = tGenerateSample( i );
					File.WriteAllBytes( strPath, bData );
				}
				catch
				{
				}
			}
		}

		private static byte[] tGenerateSample( int nLane )
		{
			const int nSampleRate = 22050;
			double dbLength;
			switch( nLane )
			{
				case 0: dbLength = 0.22; break;
				case 1: dbLength = 0.18; break;
				case 2: dbLength = 0.06; break;
				case 3: dbLength = 0.25; break;
				default: dbLength = 0.60; break;
			}
			int nCount = (int)( nSampleRate * dbLength );
			short[] arSample = new short[ nCount ];
			Random random = new Random( 12345 + nLane );
			double dbPrevNoise = 0.0;
			for( int i = 0; i < nCount; i++ )
			{
				double dbTime = (double)i / nSampleRate;
				double dbValue = 0.0;
				double dbNoise = ( random.NextDouble() * 2.0 ) - 1.0;
				switch( nLane )
				{
					case 0:
					{
						double dbFreq = 150.0 - ( 110.0 * dbTime / dbLength );
						dbValue = Math.Sin( 2.0 * Math.PI * dbFreq * dbTime ) * Math.Exp( -12.0 * dbTime );
						break;
					}
					case 1:
					{
						double dbTone = Math.Sin( 2.0 * Math.PI * 180.0 * dbTime ) * Math.Exp( -25.0 * dbTime );
						dbValue = ( 0.7 * dbNoise * Math.Exp( -22.0 * dbTime ) ) + ( 0.3 * dbTone );
						break;
					}
					case 2:
					{
						double dbHigh = dbNoise - dbPrevNoise;
						dbPrevNoise = dbNoise;
						dbValue = 0.8 * dbHigh * Math.Exp( -60.0 * dbTime );
						break;
					}
					case 3:
					{
						double dbFreq = 150.0 - ( 40.0 * dbTime / dbLength );
						dbValue = Math.Sin( 2.0 * Math.PI * dbFreq * dbTime ) * Math.Exp( -10.0 * dbTime );
						break;
					}
					default:
					{
						double dbHigh = dbNoise - ( 0.5 * dbPrevNoise );
						dbPrevNoise = dbNoise;
						dbValue = 0.5 * dbHigh * Math.Exp( -6.0 * dbTime );
						break;
					}
				}
				dbValue = Math.Max( -1.0, Math.Min( 1.0, dbValue ) );
				arSample[ i ] = (short)( dbValue * 28000.0 );
			}
			return tBuildWavFile( arSample, nSampleRate );
		}

		private static byte[] tBuildWavFile( short[] arSample, int nSampleRate )
		{
			int nDataSize = arSample.Length * 2;
			byte[] bWav = new byte[ 44 + nDataSize ];
			using( MemoryStream ms = new MemoryStream( bWav ) )
			using( BinaryWriter bw = new BinaryWriter( ms ) )
			{
				bw.Write( new char[] { 'R', 'I', 'F', 'F' } );
				bw.Write( 36 + nDataSize );
				bw.Write( new char[] { 'W', 'A', 'V', 'E' } );
				bw.Write( new char[] { 'f', 'm', 't', ' ' } );
				bw.Write( 16 );
				bw.Write( (short)1 );
				bw.Write( (short)1 );
				bw.Write( nSampleRate );
				bw.Write( nSampleRate * 2 );
				bw.Write( (short)2 );
				bw.Write( (short)16 );
				bw.Write( new char[] { 'd', 'a', 't', 'a' } );
				bw.Write( nDataSize );
				for( int i = 0; i < arSample.Length; i++ )
				{
					bw.Write( arSample[ i ] );
				}
			}
			return bWav;
		}
		#endregion

		private static string tSanitize( string strText )
		{
			if( string.IsNullOrEmpty( strText ) )
			{
				return "";
			}
			return strText.Replace( "\r", " " ).Replace( "\n", " " ).Trim();
		}
	}
}
