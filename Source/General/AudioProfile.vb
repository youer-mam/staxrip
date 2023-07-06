
Imports System.Text.RegularExpressions
Imports System.Text

Imports StaxRip.UI

<Serializable()>
Public MustInherit Class AudioProfile
    Inherits Profile

    Property Language As New Language
    Property Delay As Integer
    Property Depth As Integer = 24
    Property StreamName As String = ""
    Property Gain As Single
    Property Streams As List(Of AudioStream) = New List(Of AudioStream)
    Property [Default] As Boolean
    Property Forced As Boolean
    Property ExtractDTSCore As Boolean
    Property Decoder As AudioDecoderMode
    Property DecodingMode As AudioDecodingMode

    Overridable Property Channels As Integer = 6
    Overridable Property OutputFileType As String = "unknown"
    Overridable Property Bitrate As Double
    Overridable Property SupportedInput As String()

    Overridable Property CommandLines As String

    Sub New(name As String)
        MyBase.New(name)
    End Sub

    Sub New(name As String,
            bitrate As Integer,
            input As String(),
            fileType As String,
            channels As Integer)

        MyBase.New(name)

        Me.Channels = channels
        Me.Bitrate = bitrate
        SupportedInput = input
        OutputFileType = fileType
    End Sub

    ReadOnly Property AudioCodec As AudioCodec
        Get
            If TypeOf Me Is GUIAudioProfile Then
                Return DirectCast(Me, GUIAudioProfile).Params.Codec
            End If

            For Each i As AudioCodec In System.Enum.GetValues(GetType(AudioCodec))
                If i.ToString.ToLowerInvariant = OutputFileType Then
                    Return i
                End If
            Next

            Return AudioCodec.None
        End Get
    End Property

    Private FileValue As String = ""

    Property File As String
        Get
            Return FileValue
        End Get
        Set(value As String)
            If FileValue <> value Then
                FileValue = value
                Stream = Nothing
                OnFileChanged()
            End If
        End Set
    End Property

    Private StreamValue As AudioStream

    Property Stream As AudioStream
        Get
            Return StreamValue
        End Get
        Set(value As AudioStream)
            If Not value Is StreamValue Then
                StreamValue = value

                If Not Stream Is Nothing Then
                    If Not p.Script.GetFilter("Source").Script.Contains("DirectShowSource") Then
                        Delay = Stream.Delay
                    End If

                    Language = Stream.Language
                    Forced = Stream.Forced
                    Me.Default = Stream.Default

                    If StreamName = "" AndAlso Stream.Title <> "" Then
                        StreamName = Stream.Title
                    End If
                End If

                OnStreamChanged()
            End If
        End Set
    End Property

    Property DisplayName As String
        Get
            Dim ret As String

            If Stream Is Nothing Then
                Dim streams = MediaInfo.GetAudioStreams(File)

                If streams.Count > 0 Then
                    Dim firstStream = streams(0)

                    If firstStream.Bitrate = 0 AndAlso firstStream.Bitrate2 = 0 Then
                        firstStream.Bitrate = CInt(Calc.GetBitrateFromFile(File, p.SourceSeconds))
                    End If

                    ret = GetAudioText(firstStream, File)
                Else
                    ret = File.FileName
                End If
            Else
                ret = Stream.Name + " (" + File.Ext + ")"
            End If

            Return ret
        End Get
        Set(value As String)
        End Set
    End Property

    Private SourceSamplingRateValue As Integer

    ReadOnly Property SourceSamplingRate As Integer
        Get
            If SourceSamplingRateValue = 0 Then
                If Stream Is Nothing Then
                    If File <> "" AndAlso IO.File.Exists(File) Then
                        SourceSamplingRateValue = MediaInfo.GetAudio(File, "SamplingRate").ToInt
                    End If
                Else
                    SourceSamplingRateValue = Stream.SamplingRate
                End If
            End If

            If SourceSamplingRateValue = 0 Then
                SourceSamplingRateValue = 48000
            End If

            Return SourceSamplingRateValue
        End Get
    End Property

    ReadOnly Property HasStream As Boolean
        Get
            Return Stream IsNot Nothing
        End Get
    End Property

    Overridable Sub Migrate()
        If Depth = 0 Then
            Depth = 24
        End If
    End Sub

    ReadOnly Property ConvertExt As String
        Get
            Dim ret As String

            Select Case DecodingMode
                Case AudioDecodingMode.WAVE
                    ret = "wav"
                Case AudioDecodingMode.W64
                    ret = "w64"
                Case Else
                    ret = "flac"
            End Select

            If Not SupportedInput.Contains(ret) Then
                ret = "flac"
            End If

            If Not SupportedInput.Contains(ret) Then
                ret = "w64"
            End If

            If Not SupportedInput.Contains(ret) Then
                ret = "wav"
            End If

            Return ret
        End Get
    End Property

    Overridable Sub OnFileChanged()
    End Sub

    Overridable Sub OnStreamChanged()
    End Sub

    Function ContainsCommand(value As String) As Boolean
        Return CommandLines.ContainsEx(value)
    End Function

    Function IsUsedAndContainsCommand(value As String) As Boolean
        Return File <> "" AndAlso CommandLines.ContainsEx(value)
    End Function

    Function GetDuration() As TimeSpan
        If IO.File.Exists(File) Then
            If Stream Is Nothing Then
                Return TimeSpan.FromMilliseconds(MediaInfo.GetAudio(File, "Duration").ToDouble)
            Else
                Using mi As New MediaInfo(File)
                    Return TimeSpan.FromMilliseconds(mi.GetAudio(Stream.Index, "Duration").ToDouble)
                End Using
            End If
        End If
    End Function

    Function GetAudioText(stream As AudioStream, path As String) As String
        For Each i In Language.Languages
            If path.Contains(i.CultureInfo.EnglishName) Then
                stream.Language = i
                Exit For
            End If
        Next

        Dim matchDelay = Regex.Match(path, " (-?\d+)ms")

        If matchDelay.Success Then
            stream.Delay = matchDelay.Groups(1).Value.ToInt
        End If

        Dim matchID = Regex.Match(path, " ID(\d+)")
        Dim name As String
        name = stream.Name.Substring(3)

        If File.Base = p.SourceFile.Base Then
            Return name + " (" + File.Ext + ")"
        Else
            Return name + " (" + File.FileName + ")"
        End If
    End Function

    Sub SetStreamOrLanguage()
        If File = "" Then
            Exit Sub
        End If

        If File <> p.LastOriginalSourceFile Then
            For Each i In Language.Languages
                If File.Contains(i.CultureInfo.EnglishName) Then
                    Language = i
                    Exit Sub
                End If
            Next
        Else
            For Each i In Streams
                If FileTypes.AudioHQ.Contains(i.Ext) AndAlso i.Language.Equals(Language) Then
                    Stream = i
                    Exit For
                End If
            Next

            If Stream Is Nothing Then
                For Each i In Streams
                    If Not FileTypes.AudioHQ.Contains(i.Ext) AndAlso i.Language.Equals(Language) Then
                        Stream = i
                        Exit For
                    End If
                Next
            End If

            If Stream Is Nothing AndAlso Streams.Count > 0 Then
                Stream = Streams(0)
            End If
        End If
    End Sub

    Function IsInputSupported() As Boolean
        Return SupportedInput.NothingOrEmpty OrElse SupportedInput.Contains(File.Ext)
    End Function

    Function IsMuxProfile() As Boolean
        Return TypeOf Me Is MuxAudioProfile
    End Function

    Overridable Sub Encode()
    End Sub

    Overridable Sub EditProject()
    End Sub

    Overridable Function HandlesDelay() As Boolean
    End Function

    Function GetTrackIndex() As Integer
        If Me Is p.Audio0 Then Return 0
        If Me Is p.Audio1 Then Return 1

        For x = 0 To p.AudioTracks.Count - 1
            If Me Is p.AudioTracks(x) Then
                Return x + 2
            End If
        Next
    End Function

    Function GetTrackID() As String
        Return "_" & (CRC32.GetChecksum(p.TargetFile) + GetTrackIndex())
    End Function

    Function GetOutputFile() As String
        Dim base As String

        If HasStream Then
            base = Audio.GetBaseNameForStream(File, Stream)
        ElseIf p.TempDir.EndsWithEx("_temp\") AndAlso File.Base.StartsWithEx(p.SourceFile.Base) Then
            base = File.Base.Substring(p.SourceFile.Base.Length).Trim
        Else
            base = File.Base
        End If

        If base = "" Then
            base = "temp"
        End If

        If Not base.Contains(GetTrackID) Then
            base += GetTrackID()
        End If

        If Delay <> 0 Then
            If HandlesDelay() Then
                If base.Contains("ms") Then
                    Dim re As New Regex(" (-?\d+)ms")

                    If re.IsMatch(base) Then
                        base = re.Replace(base, "")
                    End If
                End If
            Else
                If Not base.Contains("ms") Then
                    base += " " & Delay & "ms"
                End If
            End If
        End If

        Dim outfile = p.TempDir + base + "." + OutputFileType.ToLowerInvariant

        If File = outfile Then
            outfile = outfile.Base + "_new." + OutputFileType.ToLowerInvariant
        End If

        Return outfile
    End Function

    Shared Function GetProfiles() As IEnumerable(Of AudioProfile)
        If p.AudioTracks.Count = 0 Then
            Return {p.Audio0, p.Audio1}
        Else
            Dim ret As New List(Of AudioProfile)
            ret.Add(p.Audio0)
            ret.Add(p.Audio1)
            ret.AddRange(p.AudioTracks)
            Return ret
        End If
    End Function

    Function ExpandMacros(value As String) As String
        Return ExpandMacros(value, True)
    End Function

    Function ExpandMacros(value As String, silent As Boolean) As String
        If value = "" Then Return ""
        If value.Contains("%input%") Then value = value.Replace("%input%", File.Escape)
        If value.Contains("%output%") Then value = value.Replace("%output%", GetOutputFile.Escape)
        If value.Contains("%bitrate%") Then value = value.Replace("%bitrate%", Bitrate.ToString)
        If value.Contains("%channels%") Then value = value.Replace("%channels%", Channels.ToString)
        If value.Contains("%language_native%") Then value = value.Replace("%language_native%", Language.CultureInfo.NativeName)
        If value.Contains("%language_english%") Then value = value.Replace("%language_english%", Language.Name)
        If value.Contains("%delay%") Then value = value.Replace("%delay%", Delay.ToString)
        If value.Contains("%streamid0%") AndAlso Stream IsNot Nothing Then value = value.Replace("%streamid0%", (Stream?.ID - 1)?.ToString)
        If value.Contains("%streamid1%") Then value = value.Replace("%streamid1%", Stream?.ID.ToString)

        Return Macro.Expand(value)
    End Function

    Shared Function GetDefaults() As List(Of AudioProfile)
        Dim ret As New List(Of AudioProfile)

        ret.Add(New GUIAudioProfile(AudioCodec.AAC, 50))
        ret.Add(New GUIAudioProfile(AudioCodec.Opus, 1) With {.Bitrate = 250})
        ret.Add(New GUIAudioProfile(AudioCodec.FLAC, 0.3))
        ret.Add(New GUIAudioProfile(AudioCodec.Vorbis, 1))
        ret.Add(New GUIAudioProfile(AudioCodec.MP3, 4))
        ret.Add(New GUIAudioProfile(AudioCodec.AC3, 1.0) With {.Channels = 6, .Bitrate = 640})
        ret.Add(New GUIAudioProfile(AudioCodec.EAC3, 1.0) With {.Channels = 6, .Bitrate = 640})
        ret.Add(New BatchAudioProfile(640, {}, "ac3", 6, """%app:ffmpeg%"" -i %input% -b:a %bitrate%k -y -hide_banner %output%"))
        ret.Add(New MuxAudioProfile())
        ret.Add(New NullAudioProfile())

        Return ret
    End Function
End Class

<Serializable()>
Public Class BatchAudioProfile
    Inherits AudioProfile

    Sub New(bitrate As Integer,
            input As String(),
            fileType As String,
            channels As Integer,
            commandLines As String)

        MyBase.New("Command Line", bitrate, input, fileType, channels)
        Me.CommandLines = commandLines
        CanEditValue = True
    End Sub

    Overrides Function Edit() As DialogResult
        Using form As New CommandLineAudioEncoderForm(Me)
            form.mbLanguage.Enabled = False
            form.lLanguage.Enabled = False
            form.tbDelay.Enabled = False
            form.lDelay.Enabled = False
            Return form.ShowDialog()
        End Using
    End Function

    Function GetCode() As String
        Return ExpandMacros(CommandLines).Trim
    End Function

    Overrides Sub Encode()
        If File <> "" Then
            Dim bitrateBefore = p.VideoBitrate
            Dim targetPath = GetOutputFile()

            For Each line In Macro.Expand(GetCode).SplitLinesNoEmpty
                Using proc As New Proc
                    proc.Header = "Audio Encoding: " + Name
                    proc.SkipStrings = Proc.GetSkipStrings(CommandLines)
                    proc.File = "cmd.exe"
                    proc.Arguments = "/S /C """ + line + """"

                    Try
                        proc.Start()
                    Catch ex As AbortException
                        Throw ex
                    Catch ex As Exception
                        g.ShowException(ex)
                        Throw New AbortException
                    End Try
                End Using
            Next

            If g.FileExists(targetPath) Then
                If Not p.BitrateIsFixed Then
                    Dim bitrateFromFile = Calc.GetBitrateFromFile(targetPath, p.TargetSeconds)
                    p.VideoBitrate = p.VideoBitrate + CInt(Calc.GetAudioBitrateFromAudioProfile(Me)) - CInt(bitrateFromFile)
                    Bitrate = bitrateFromFile

                    If Not p.VideoEncoder.QualityMode Then
                        Log.WriteLine("Video Bitrate Adjustment: " + bitrateBefore.ToString() + " -> " & p.VideoBitrate & BR2)
                    End If
                End If

                File = targetPath
                Log.WriteLine(MediaInfo.GetSummary(File))
            Else
                Log.Write("Error", "no output found")

                If Not File.Ext = "wav" Then
                    Audio.Convert(Me)

                    If File.Ext = "wav" Then
                        Encode()
                    End If
                End If
            End If
        End If
    End Sub

    Overrides Sub EditProject()
        Using f As New CommandLineAudioEncoderForm(Me)
            f.ShowDialog()
        End Using
    End Sub

    Overrides Function HandlesDelay() As Boolean
        Return CommandLines.Contains("%delay%")
    End Function
End Class

<Serializable()>
Public Class NullAudioProfile
    Inherits AudioProfile

    Sub New()
        MyBase.New("No Audio", 0, {}, "ignore", 0)
    End Sub

    Overrides Function HandlesDelay() As Boolean
    End Function

    Overrides Sub EditProject()
        Using form As New SimpleSettingsForm("Null Audio Profile Options")
            form.ScaleClientSize(20, 10)
            Dim ui = form.SimpleUI
            ui.Store = Me

            Dim n = ui.AddNum()
            n.Text = "Reserved Bitrate:"
            n.Config = {0, Integer.MaxValue, 8}
            n.Property = NameOf(Bitrate)

            If form.ShowDialog() = DialogResult.OK Then
                ui.Save()
            End If
        End Using
    End Sub

    Public Overrides Property OutputFileType As String
        Get
            Return "ignore"
        End Get
        Set(value As String)
        End Set
    End Property

    Public Overrides Sub Encode()
    End Sub
End Class

<Serializable()>
Public Class MuxAudioProfile
    Inherits AudioProfile

    Sub New()
        MyBase.New("Copy/Mux", 0, Nothing, "ignore", 2)
        CanEditValue = True
    End Sub

    Private ChannelsValue As Integer = 0

    Public Overrides Property Channels As Integer
        Get
            If Not Stream Is Nothing Then
                If Stream.Channels > Stream.Channels2 Then
                    Return Stream.Channels
                Else
                    Return Stream.Channels2
                End If
            ElseIf IO.File.Exists(File) Then

                If ChannelsValue = 0 Then
                    ChannelsValue = MediaInfo.GetChannels(File)
                End If

                Return ChannelsValue
            Else
                Return 6
            End If
        End Get
        Set(value As Integer)
        End Set
    End Property

    Public Overrides Property OutputFileType As String
        Get
            If Stream Is Nothing Then
                Return File.Ext
            Else
                Return Stream.Ext
            End If
        End Get
        Set(value As String)
        End Set
    End Property

    Overrides Property SupportedInput As String()
        Get
            Return {}
        End Get
        Set(value As String())
        End Set
    End Property

    Overrides Function Edit() As DialogResult
        Return Edit(False)
    End Function

    Overrides Sub EditProject()
        Edit(True)
    End Sub

    Overrides Sub Encode()
    End Sub

    Overrides Sub OnFileChanged()
        MyBase.OnFileChanged()
        SetBitrate()
    End Sub

    Overrides Sub OnStreamChanged()
        MyBase.OnStreamChanged()
        SetBitrate()
    End Sub

    Sub SetBitrate()
        If Stream Is Nothing Then
            Dim temp = CInt(MediaInfo.GetAudio(File, "BitRate").ToInt / 1000)

            If temp > 0 Then
                Bitrate = temp
            Else
                Bitrate = Calc.GetBitrateFromFile(File, p.SourceSeconds)
            End If
        Else
            Bitrate = Stream.Bitrate + Stream.Bitrate2
        End If
    End Sub

    Private Overloads Function Edit(showProjectSettings As Boolean) As DialogResult
        Using form As New SimpleSettingsForm("Audio Mux Options",
            "The Audio Mux options allow to add an audio file without reencoding.")

            form.ScaleClientSize(30, 15)

            Dim ui = form.SimpleUI
            Dim page = ui.CreateFlowPage("main page")
            page.SuspendLayout()

            Dim tbb = ui.AddTextButton(page)
            tbb.Label.Text = "Track Name:"
            tbb.Label.Help = "Track name used by the muxer. The track name may contain macros."
            tbb.Edit.Expand = True
            tbb.Edit.Text = StreamName
            tbb.Edit.SaveAction = Sub(value) StreamName = value
            tbb.Button.Text = "Macro Editor..."
            tbb.Button.ClickAction = AddressOf tbb.Edit.EditMacro

            Dim nb = ui.AddNum(page)
            nb.Label.Text = "Delay:"
            nb.Label.Help = "Delay used by the muxer."
            nb.NumEdit.Config = {Integer.MinValue, Integer.MaxValue, 1}
            nb.NumEdit.Value = Delay
            nb.NumEdit.SaveAction = Sub(value) Delay = CInt(value)

            Dim mbi = ui.AddMenu(Of Language)(page)
            mbi.Label.Text = "Language:"
            mbi.Label.Help = "Language of the audio track."
            mbi.Button.Value = Language
            mbi.Button.SaveAction = Sub(value) Language = value

            For Each i In Language.Languages
                If i.IsCommon Then
                    mbi.Button.Add(i.ToString, i)
                Else
                    mbi.Button.Add("More | " + i.ToString.Substring(0, 1).ToUpperInvariant + " | " + i.ToString, i)
                End If
            Next

            Dim cb = ui.AddBool(page)
            cb.Text = "Default"
            cb.Help = "Flaged as default."
            cb.Checked = [Default]
            cb.SaveAction = Sub(value) [Default] = value

            If TypeOf p.VideoEncoder.Muxer Is MkvMuxer Then
                cb = ui.AddBool(page)
                cb.Text = "Forced"
                cb.Help = "Flaged as forced in MKV."
                cb.Checked = Forced
                cb.SaveAction = Sub(value) Forced = value
            End If

            cb = ui.AddBool(page)
            cb.Text = "Extract DTS Core"
            cb.Help = "Only include DTS core using mkvmerge."
            cb.Checked = ExtractDTSCore
            cb.SaveAction = Sub(value) ExtractDTSCore = value

            page.ResumeLayout()

            Dim ret = form.ShowDialog()

            If ret = DialogResult.OK Then
                ui.Save()
            End If

            Return ret
        End Using
    End Function
End Class

<Serializable()>
Public Class GUIAudioProfile
    Inherits AudioProfile

    Property Params As New Parameters

    Sub New(codec As AudioCodec, quality As Single)
        MyBase.New(Nothing)

        Params.Codec = codec
        Params.Quality = quality

        Select Case codec
            Case AudioCodec.DTS, AudioCodec.AC3, AudioCodec.EAC3
                Params.RateMode = AudioRateMode.CBR
            Case Else
                Params.RateMode = AudioRateMode.VBR
        End Select

        Bitrate = GetBitrate()
    End Sub

    Private ChannelsValue As Integer

    Public Overrides Property Channels As Integer
        Get
            Select Case Params.ChannelsMode
                Case ChannelsMode.Original
                    If Not Stream Is Nothing Then
                        If Stream.Channels > Stream.Channels2 Then
                            Return Stream.Channels
                        Else
                            Return Stream.Channels2
                        End If
                    ElseIf IO.File.Exists(File) Then
                        If ChannelsValue = 0 Then
                            ChannelsValue = MediaInfo.GetChannels(File)
                        End If

                        Return ChannelsValue
                    Else
                        Return 6
                    End If
                Case ChannelsMode._1
                    Return 1
                Case ChannelsMode._2
                    Return 2
                Case ChannelsMode._6
                    Return 6
                Case ChannelsMode._7
                    Return 7
                Case ChannelsMode._8
                    Return 8
                Case Else
                    Throw New NotImplementedException
            End Select
        End Get
        Set(value As Integer)
        End Set
    End Property

    ReadOnly Property TargetSamplingRate As Integer
        Get
            If Params.SamplingRate <> 0 Then
                Return Params.SamplingRate
            Else
                Return SourceSamplingRate
            End If
        End Get
    End Property

    Public Overrides Sub Migrate()
        MyBase.Migrate()
        Params.Migrate()
    End Sub

    Public Function GetBitrate() As Integer
        If Params.RateMode = AudioRateMode.VBR Then
            Select Case Params.Codec
                Case AudioCodec.AAC
                    Select Case Params.Encoder
                        Case GuiAudioEncoder.qaac, GuiAudioEncoder.Automatic
                            Return Calc.GetYFromTwoPointForm(0, CInt(50 / 8 * Channels), 127, CInt(1000 / 8 * Channels), Params.Quality)
                        Case GuiAudioEncoder.eac3to
                            Return Calc.GetYFromTwoPointForm(0.01, CInt(50 / 8 * Channels), 1, CInt(1000 / 8 * Channels), Params.Quality)
                        Case Else
                            Return Calc.GetYFromTwoPointForm(1, CInt(50 / 8 * Channels), 5, CInt(900 / 8 * Channels), Params.Quality)
                    End Select
                Case AudioCodec.MP3
                    Return Calc.GetYFromTwoPointForm(9, 65, 0, 245, Params.Quality)
                Case AudioCodec.Vorbis
                    If Channels >= 6 Then
                        Return Calc.GetYFromTwoPointForm(0, 120, 10, 1440, Params.Quality)
                    Else
                        Return Calc.GetYFromTwoPointForm(0, 64, 10, 500, Params.Quality)
                    End If
                Case AudioCodec.Opus
                    Return CInt(Bitrate)
            End Select
        End If

        Select Case Params.Codec
            Case AudioCodec.FLAC
                Return CInt(((TargetSamplingRate * Depth * Channels) / 1000) * 0.55)
            Case AudioCodec.W64, AudioCodec.WAV
                Return CInt((TargetSamplingRate * Depth * Channels) / 1000)
        End Select

        Return CInt(Bitrate)
    End Function

    Public Overrides Sub Encode()
        If File <> "" Then
            Dim bitrateBefore = p.VideoBitrate
            Dim targetPath = GetOutputFile()
            Dim cl = GetCommandLine(True)

            Using proc As New Proc
                proc.Header = "Audio Encoding " & (GetTrackIndex() + 1)

                If cl.Contains("|") Then
                    proc.File = "cmd.exe"
                    proc.Arguments = "/S /C """ + cl + """"
                Else
                    proc.CommandLine = cl
                End If

                If cl.Contains("qaac64") Then
                    proc.Package = Package.qaac
                    proc.SkipStrings = {", ETA ", "x)", "Optimizing..."}
                    proc.AddToPath(Package.AppleApplicationSupport.Directory)
                ElseIf cl.Contains("fdkaac") Then
                    proc.Package = Package.fdkaac
                    proc.SkipStrings = {"%]", "x)"}
                ElseIf cl.Contains("eac3to") Then
                    proc.Package = Package.eac3to
                    proc.SkipStrings = {"process: ", "analyze: "}
                    proc.TrimChars = {"-"c, " "c}
                    proc.AddToPath(Package.NeroAAC.Directory)
                ElseIf cl.Contains("ffmpeg") Then
                    If cl.Contains("libfdk_aac") Then
                        proc.Package = Package.ffmpeg_non_free
                    Else
                        proc.Package = Package.ffmpeg
                    End If

                    proc.SkipStrings = {"frame=", "size="}
                    proc.Encoding = Encoding.UTF8
                    proc.Duration = GetDuration()
                End If

                proc.Start()
            End Using

            If g.FileExists(targetPath) Then
                If Not p.BitrateIsFixed Then
                    Dim bitrateFromFile = Calc.GetBitrateFromFile(targetPath, p.TargetSeconds)
                    p.VideoBitrate = p.VideoBitrate + CInt(Calc.GetAudioBitrateFromAudioProfile(Me)) - CInt(bitrateFromFile)
                    Bitrate = bitrateFromFile

                    If Not p.VideoEncoder.QualityMode Then
                        Log.WriteLine("Video Bitrate Adjustment: " + bitrateBefore.ToString() + " -> " & p.VideoBitrate & BR2)
                    End If
                End If

                File = targetPath
                Log.WriteLine(MediaInfo.GetSummary(File))
            Else
                Throw New ErrorAbortException("Error audio encoding", "The output file is missing")
            End If
        End If
    End Sub

    Sub NormalizeFF()
        If Not Params.Normalize OrElse ExtractCore OrElse
            Not {ffmpegNormalizeMode.loudnorm, ffmpegNormalizeMode.volumedetect}.Contains(Params.ffmpegNormalizeMode) Then

            Exit Sub
        End If

        Dim args = "-i " + File.Escape

        If Not Stream Is Nothing AndAlso Streams.Count > 1 Then
            args += " -map 0:a:" & Stream.Index
        End If

        args += " -sn -vn -hide_banner"

        If Params.ffmpegNormalizeMode = ffmpegNormalizeMode.volumedetect Then
            args += " -af volumedetect"
        ElseIf Params.ffmpegNormalizeMode = ffmpegNormalizeMode.loudnorm Then
            args += " -af loudnorm=I=" & Params.ffmpegLoudnormIntegrated.ToInvariantString +
                ":TP=" & Params.ffmpegLoudnormTruePeak.ToInvariantString + ":LRA=" &
                Params.ffmpegLoudnormLRA.ToInvariantString + ":print_format=summary"
        End If

        args += " -f null NUL"

        Using proc As New Proc
            proc.Header = "Find Gain " & (GetTrackIndex() + 1)
            proc.SkipStrings = {"frame=", "size="}
            proc.Encoding = Encoding.UTF8
            proc.Package = Package.ffmpeg
            proc.Arguments = args
            proc.Start()

            Dim match = Regex.Match(proc.Log.ToString, "max_volume: -(\d+\.\d+) dB")
            If match.Success Then Gain += match.Groups(1).Value.ToSingle()

            match = Regex.Match(proc.Log.ToString, "Input Integrated:\s*([-\.0-9]+)")
            If match.Success Then Params.ffmpegLoudnormIntegratedMeasured = match.Groups(1).Value.ToDouble

            match = Regex.Match(proc.Log.ToString, "Input True Peak:\s*([-\.0-9]+)")
            If match.Success Then Params.ffmpegLoudnormTruePeakMeasured = match.Groups(1).Value.ToDouble

            match = Regex.Match(proc.Log.ToString, "Input LRA:\s*([-\.0-9]+)")
            If match.Success Then Params.ffmpegLoudnormLraMeasured = match.Groups(1).Value.ToDouble

            match = Regex.Match(proc.Log.ToString, "Input Threshold:\s*([-\.0-9]+)")
            If match.Success Then Params.ffmpegLoudnormThresholdMeasured = match.Groups(1).Value.ToDouble
        End Using
    End Sub

    Overrides Function Edit() As DialogResult
        Using form As New AudioForm()
            form.LoadProfile(Me)
            form.mbLanguage.Enabled = False
            form.numDelay.Enabled = False
            form.numGain.Enabled = False
            Return form.ShowDialog()
        End Using
    End Function

    Overrides Sub EditProject()
        Using form As New AudioForm()
            form.LoadProfile(Me)
            form.ShowDialog()
        End Using
    End Sub

    Public Overrides Property OutputFileType As String
        Get
            Select Case Params.Codec
                Case AudioCodec.AAC
                    Return "m4a"
                Case AudioCodec.Vorbis
                    Return "ogg"
                Case Else
                    Return Params.Codec.ToString.ToLowerInvariant
            End Select
        End Get
        Set(value As String)
        End Set
    End Property

    Function GetEac3toCommandLine(includePaths As Boolean) As String
        Dim id = ""
        Dim sb As New StringBuilder()

        If File.Ext.EqualsAny("ts", "m2ts", "mkv") AndAlso Not Stream Is Nothing Then
            id = (Stream.StreamOrder + 1) & ": "
        End If

        If includePaths Then
            sb.Append(Package.eac3to.Path.Escape + " " + id + File.LongPathPrefix.Escape +
                " " + GetOutputFile.LongPathPrefix.Escape)
        Else
            sb.Append("eac3to")
        End If

        If Not (Params.Codec = AudioCodec.DTS AndAlso ExtractDTSCore) Then
            Select Case Params.Codec
                Case AudioCodec.AAC
                    sb.Append(" -quality=" & Params.Quality.ToInvariantString)
                Case AudioCodec.AC3
                    sb.Append(" -" & Bitrate)
                Case AudioCodec.DTS
                    sb.Append(" -" & Bitrate)
            End Select

            If Params.Normalize Then
                sb.Append(" -normalize")
            End If

            If Depth = 16 Then
                sb.Append(" -down16")
            End If

            If Params.SamplingRate <> 0 Then
                sb.Append(" -resampleTo" & Params.SamplingRate)
            End If

            If Params.FrameRateMode = AudioFrameRateMode.Speedup Then
                sb.Append(" -speedup")
            End If

            If Params.FrameRateMode = AudioFrameRateMode.Slowdown Then
                sb.Append(" -slowdown")
            End If

            If Delay <> 0 Then
                sb.Append(" " + If(Delay > 0, "+", "") & Delay & "ms")
            End If

            If Gain < 0 Then
                sb.Append(" " & CInt(Gain) & "dB")
            End If

            If Gain > 0 Then
                sb.Append(" +" & CInt(Gain) & "dB")
            End If

            Select Case Channels
                Case 6
                    If Params.ChannelsMode <> ChannelsMode.Original Then
                        sb.Append(" -down6")
                    End If
                Case 2
                    If Params.eac3toStereoDownmixMode = 0 Then
                        If Params.ChannelsMode <> ChannelsMode.Original Then
                            sb.Append(" -downStereo")
                        End If
                    Else
                        sb.Append(" -downDpl")
                    End If
            End Select

            If Params.CustomSwitches <> "" Then
                sb.Append(" " + Params.CustomSwitches)
            End If
        ElseIf ExtractDTSCore Then
            sb.Append(" -core")
        End If

        If includePaths Then
            sb.Append(" -progressnumbers")
        End If

        Return sb.ToString
    End Function

    Function GetfdkaacCommandLine(includePaths As Boolean) As String
        Dim sb As New StringBuilder
        includePaths = includePaths And File <> ""

        If DecodingMode = AudioDecodingMode.Pipe Then
            sb.Append(GetPipeCommandLine(includePaths))
        End If

        If includePaths Then
            sb.Append(Package.fdkaac.Path.Escape)
        Else
            sb.Clear()
            sb.Append("fdkaac")
        End If

        If Params.fdkaacProfile <> 2 Then
            sb.Append(" --profile " & Params.fdkaacProfile)
        End If

        If Params.SimpleRateMode = SimpleAudioRateMode.CBR Then
            sb.Append(" --bitrate " & CInt(Bitrate))
        Else
            sb.Append(" --bitrate-mode " & Params.Quality)
        End If

        If Params.fdkaacGaplessMode <> 0 Then sb.Append(" --gapless-mode " & Params.fdkaacGaplessMode)
        If Params.fdkaacBandwidth <> 0 Then sb.Append(" --bandwidth " & Params.fdkaacBandwidth)
        If Not Params.fdkaacAfterburner Then sb.Append(" --afterburner 0")
        If Params.fdkaacAdtsCrcCheck Then sb.Append(" --adts-crc-check")
        If Params.fdkaacMoovBeforeMdat Then sb.Append(" --moov-before-mdat")
        If Params.fdkaacIncludeSbrDelay Then sb.Append(" --include-sbr-delay")
        If Params.fdkaacHeaderPeriod Then sb.Append(" --header-period")
        If Params.fdkaacLowDelaySBR <> 0 Then sb.Append(" --lowdelay-sbr " & Params.fdkaacLowDelaySBR)
        If Params.fdkaacSbrRatio <> 0 Then sb.Append(" --sbr-ratio " & Params.fdkaacSbrRatio)
        If Params.fdkaacTransportFormat <> 0 Then sb.Append(" --transport-format " & Params.fdkaacTransportFormat)
        If Params.CustomSwitches <> "" Then sb.Append(" " + Params.CustomSwitches)

        Dim input = If(DecodingMode = AudioDecodingMode.Pipe, "-", File.LongPathPrefix.Escape)

        If includePaths Then
            sb.Append(" --ignorelength -o " + GetOutputFile.LongPathPrefix.Escape + " " + input)
        End If

        Return sb.ToString
    End Function

    Function GetQaacCommandLine(includePaths As Boolean) As String
        Dim usePipe = DecodingMode = AudioDecodingMode.Pipe OrElse (Params.ChannelsMode <> ChannelsMode.Original AndAlso SupportedInput.Contains(File.Ext))
        Dim sb As New StringBuilder
        includePaths = includePaths And File <> ""

        If usePipe Then
            sb.Append(GetPipeCommandLine(includePaths))
        End If

        If includePaths Then
            sb.Append(Package.qaac.Path.Escape)
        Else
            sb.Clear()
            sb.Append("qaac")
        End If

        Select Case Params.qaacRateMode
            Case 0
                sb.Append(" --tvbr " & CInt(Params.Quality))
            Case 1
                sb.Append(" --cvbr " & CInt(Bitrate))
            Case 2
                sb.Append(" --abr " & CInt(Bitrate))
            Case 3
                sb.Append(" --cbr " & CInt(Bitrate))
        End Select

        If Params.qaacHE AndAlso {1, 2, 3}.Contains(Params.qaacRateMode) Then
            sb.Append(" --he")
        End If

        If Delay <> 0 Then
            sb.Append(" --delay " + (Delay / 1000).ToInvariantString)
        End If

        If Params.Normalize Then
            sb.Append(" --normalize")
        End If

        If Params.qaacQuality <> 2 Then
            sb.Append(" --quality " & Params.qaacQuality)
        End If

        If Params.SamplingRate <> 0 Then
            sb.Append(" --rate " & Params.SamplingRate)
        End If

        If Params.qaacLowpass <> 0 Then
            sb.Append(" --lowpass " & Params.qaacLowpass)
        End If

        If Params.qaacNoDither Then
            sb.Append(" --no-dither")
        End If

        If Gain <> 0 Then
            sb.Append(" --gain " & Gain.ToInvariantString)
        End If

        If Params.CustomSwitches <> "" Then
            sb.Append(" " + Params.CustomSwitches)
        End If

        Dim input = If(usePipe, "-", File.Escape)

        If includePaths Then
            sb.Append(" " + input + " -o " + GetOutputFile.Escape)
        End If

        Return sb.ToString
    End Function

    Function GetPipeCommandLine(includePaths As Boolean) As String
        Dim sb As New StringBuilder

        If includePaths AndAlso File <> "" Then
            sb.Append(Package.ffmpeg.Path.Escape + " -i " + File.Escape)
        Else
            sb.Append("ffmpeg")
        End If

        If Not Stream Is Nothing AndAlso Streams.Count > 1 Then
            sb.Append(" -map 0:a:" & Stream.Index)
        End If

        If Params.ChannelsMode <> ChannelsMode.Original Then
            sb.Append(" -ac " & Channels)
        End If

        If Params.Normalize Then
            If Params.ffmpegNormalizeMode = ffmpegNormalizeMode.dynaudnorm Then
                sb.Append(" " + Audio.GetDynAudNormArgs(Params))
            ElseIf Params.ffmpegNormalizeMode = ffmpegNormalizeMode.loudnorm Then
                sb.Append(" " + Audio.GetLoudNormArgs(Params))
            End If
        End If

        If includePaths AndAlso File <> "" Then
            sb.Append(" -loglevel fatal -hide_banner -f wav - | ")
        End If

        Return sb.ToString
    End Function

    Function GetffmpegCommandLine(includePaths As Boolean) As String
        Dim sb As New StringBuilder
        Dim pack = If(Params.Codec = AudioCodec.AAC AndAlso Params.ffmpegLibFdkAAC,
            Package.ffmpeg_non_free, Package.ffmpeg)

        If includePaths AndAlso File <> "" Then
            sb.Append(pack.Path.Escape)

            If Params.ProbeSize <> 5 Then
                sb.Append($" -probesize {Params.ProbeSize}M")
            End If

            If Params.AnalyzeDuration <> 5 Then
                sb.Append($" -analyzeduration {Params.AnalyzeDuration}M")
            End If

            If File.Ext.EqualsAny("avs", "vpy") Then
                sb.Append(" -vn")
            End If

            sb.Append(" -i " + File.Escape)
        Else
            sb.Append("ffmpeg")
        End If

        If Not Stream Is Nothing Then
            sb.Append(" -map 0:a:" & Stream.Index)
        End If

        Select Case Params.Codec
            Case AudioCodec.MP3
                If Not Params.CustomSwitches.Contains("-c:a ") Then
                    sb.Append(" -c:a libmp3lame")
                End If

                Select Case Params.RateMode
                    Case AudioRateMode.ABR
                        sb.Append(" -b:a " & CInt(Bitrate) & "k -abr 1")
                    Case AudioRateMode.CBR
                        sb.Append(" -b:a " & CInt(Bitrate) & "k")
                    Case AudioRateMode.VBR
                        sb.Append(" -q:a " & CInt(Params.Quality))
                End Select
            Case AudioCodec.AC3
                If Not Params.CustomSwitches.Contains("-c:a ") Then
                    sb.Append(" -c:a ac3")
                End If

                sb.Append(" -b:a " & CInt(Bitrate) & "k")
            Case AudioCodec.EAC3
                If Not Params.CustomSwitches.Contains("-c:a ") Then
                    sb.Append(" -c:a eac3")
                End If

                sb.Append(" -b:a " & If(Bitrate > 960, CInt(Bitrate) + 0.1, CInt(Bitrate)).ToInvariantString() & "k")
            Case AudioCodec.DTS
                If ExtractDTSCore Then
                    sb.Append(" -bsf:a dca_core -c:a copy")
                Else
                    sb.Append(" -strict -2 -b:a " & CInt(Bitrate) & "k")
                End If
            Case AudioCodec.Vorbis
                If Not Params.CustomSwitches.Contains("-c:a ") Then
                    sb.Append(" -c:a libvorbis")
                End If

                If Params.RateMode = AudioRateMode.VBR Then
                    sb.Append(" -q:a " & CInt(Params.Quality))
                Else
                    sb.Append(" -b:a " & CInt(Bitrate) & "k")
                End If
            Case AudioCodec.Opus
                If Not Params.CustomSwitches.Contains("-c:a ") Then
                    sb.Append(" -c:a libopus")
                End If

                Select Case Params.ffmpegOpusRateMode
                    Case OpusRateMode.CBR
                        sb.Append(" -vbr off")
                    Case OpusRateMode.CVBR
                        sb.Append(" -vbr constrained")
                End Select

                sb.Append(" -b:a " & CInt(Bitrate) & "k")

                Select Case Params.ffmpegOpusApp
                    Case OpusApp.voip
                        sb.Append(" -application voip")
                    Case OpusApp.lowdelay
                        sb.Append(" -application lowdelay")
                End Select

                If Params.ffmpegOpusFrame <> 20 Then
                    sb.Append(" -frame_duration " & Params.ffmpegOpusFrame.ToInvariantString)
                End If

                If Params.ffmpegOpusMap <> -1 Then
                    sb.Append(" -mapping_family " & CInt(Params.ffmpegOpusMap))
                End If

                If Params.ffmpegOpusCompress <> 10 Then
                    sb.Append(" -compression_level " & CInt(Params.ffmpegOpusCompress))
                End If

                If Params.ffmpegOpusPacket <> 0 Then
                    sb.Append(" -packet_loss " & CInt(Params.ffmpegOpusPacket))
                End If
            Case AudioCodec.AAC
                If Params.ffmpegLibFdkAAC Then
                    sb.Append(" -c:a libfdk_aac")

                    If Params.RateMode = SimpleAudioRateMode.CBR Then
                        sb.Append(" -b:a " & CInt(Bitrate) & "k")
                    Else
                        sb.Append(" -vbr " & CInt(Params.Quality))
                    End If
                Else
                    sb.Append(" -c:a aac")

                    If Params.RateMode = SimpleAudioRateMode.CBR Then
                        sb.Append(" -b:a " & CInt(Bitrate) & "k")
                    Else
                        sb.Append(" -q:a " & CInt(Params.Quality))
                    End If
                End If
            Case AudioCodec.W64, AudioCodec.WAV
                If Depth = 24 Then
                    sb.Append(" -c:a pcm_s24le")
                Else
                    sb.Append(" -c:a pcm_s16le")
                End If
        End Select

        If Gain <> 0 Then
            sb.Append(" -af volume=" + Gain.ToInvariantString + "dB")
        End If

        If Params.Normalize Then
            If Params.ffmpegNormalizeMode = ffmpegNormalizeMode.loudnorm Then
                sb.Append(" " + Audio.GetLoudNormArgs(Params))
            ElseIf Params.ffmpegNormalizeMode = ffmpegNormalizeMode.dynaudnorm Then
                sb.Append(" " + Audio.GetDynAudNormArgs(Params))
            End If
        End If

        If Params.ChannelsMode <> ChannelsMode.Original OrElse Params.Codec = AudioCodec.Opus Then
            sb.Append(" -ac " & Channels)
        End If

        If Params.SamplingRate <> 0 Then
            sb.Append(" -ar " & Params.SamplingRate)
        End If

        If Params.CustomSwitches <> "" Then
            sb.Append(" " + Params.CustomSwitches)
        End If

        If includePaths AndAlso File <> "" Then
            sb.Append(" -y -hide_banner")
            sb.Append(" " + GetOutputFile.Escape)
        End If

        Return sb.ToString
    End Function

    Function SupportsNormalize() As Boolean
        Return GetEncoder() = GuiAudioEncoder.eac3to OrElse GetEncoder() = GuiAudioEncoder.qaac
    End Function

    Public Overrides ReadOnly Property DefaultName As String
        Get
            If Params Is Nothing Then
                Exit Property
            End If

            Dim ch = ""

            Select Case Params.ChannelsMode
                Case ChannelsMode._8
                    ch += " 7.1"
                Case ChannelsMode._7
                    ch += " 6.1"
                Case ChannelsMode._6
                    ch += " 5.1"
                Case ChannelsMode._2
                    ch += " 2.0"
                Case ChannelsMode._1
                    ch += " Mono"
            End Select

            Dim circa = If(Params.RateMode = AudioRateMode.VBR OrElse Params.Codec = AudioCodec.FLAC, "~", "")
            Dim bitrate = If(Params.RateMode = AudioRateMode.VBR, GetBitrate(), Me.Bitrate)

            If ExtractCore Then
                Return "Extract DTS Core"
            Else
                Return Params.Codec.ToString + ch & " " & circa & bitrate & " Kbps"
            End If
        End Get
    End Property

    ReadOnly Property ExtractCore As Boolean
        Get
            Dim enc = GetEncoder()

            If Params.Codec = AudioCodec.DTS AndAlso ExtractDTSCore AndAlso
                (enc = GuiAudioEncoder.eac3to OrElse enc = GuiAudioEncoder.ffmpeg) Then

                Return True
            End If
        End Get
    End Property

    Overrides Property CommandLines() As String
        Get
            Return GetCommandLine(True)
        End Get
        Set(Value As String)
        End Set
    End Property

    Overrides ReadOnly Property CanEdit() As Boolean
        Get
            Return True
        End Get
    End Property

    Overrides Function HandlesDelay() As Boolean
        If {GuiAudioEncoder.eac3to, GuiAudioEncoder.qaac}.Contains(GetEncoder()) Then
            Return True
        End If
    End Function

    Function GetEncoder() As GuiAudioEncoder
        Select Case Params.Encoder
            Case GuiAudioEncoder.eac3to
                If {AudioCodec.AAC, AudioCodec.AC3, AudioCodec.FLAC, AudioCodec.DTS,
                    AudioCodec.W64, AudioCodec.WAV}.Contains(Params.Codec) Then

                    Return GuiAudioEncoder.eac3to
                End If
            Case GuiAudioEncoder.qaac
                If Params.Codec = AudioCodec.AAC Then
                    Return GuiAudioEncoder.qaac
                End If
            Case GuiAudioEncoder.fdkaac
                If Params.Codec = AudioCodec.AAC Then
                    Return GuiAudioEncoder.fdkaac
                End If
            Case GuiAudioEncoder.Automatic
                If Params.Codec = AudioCodec.AAC Then
                    Return GuiAudioEncoder.qaac
                End If
        End Select

        Return GuiAudioEncoder.ffmpeg
    End Function

    Function GetCommandLine(includePaths As Boolean, Optional useCrcSuffix As Boolean = False) As String
        Select Case GetEncoder()
            Case GuiAudioEncoder.eac3to
                Return GetEac3toCommandLine(includePaths)
            Case GuiAudioEncoder.qaac
                Return GetQaacCommandLine(includePaths)
            Case GuiAudioEncoder.fdkaac
                Return GetfdkaacCommandLine(includePaths)
            Case Else
                Return GetffmpegCommandLine(includePaths)
        End Select
    End Function

    Overrides Property SupportedInput As String()
        Get
            Select Case GetEncoder()
                Case GuiAudioEncoder.eac3to
                    Return FileTypes.eac3toInput
                Case GuiAudioEncoder.qaac
                    If DecodingMode <> AudioDecodingMode.Pipe Then
                        If p.Ranges.Count > 0 Then
                            Return {"wav", "w64"}
                        Else
                            Return {"wav", "flac", "w64"}
                        End If
                    End If
                Case GuiAudioEncoder.fdkaac
                    If DecodingMode <> AudioDecodingMode.Pipe Then
                        Return {"wav"}
                    End If
            End Select

            Return {}
        End Get
        Set(value As String())
        End Set
    End Property

    <Serializable()>
    Public Class Parameters
        Property AnalyzeDuration As Integer = 5
        Property ChannelsMode As ChannelsMode
        Property Codec As AudioCodec
        Property CustomSwitches As String = ""
        Property eac3toStereoDownmixMode As Integer
        Property Encoder As GuiAudioEncoder
        Property FrameRateMode As AudioFrameRateMode
        Property Normalize As Boolean = True
        Property ProbeSize As Integer = 5
        Property Quality As Single = 0.3
        Property RateMode As AudioRateMode
        Property SamplingRate As Integer

        Property Migrate1 As Boolean = True
        Property MigrateffNormalizeMode As Boolean = True

        Property qaacHE As Boolean
        Property qaacLowpass As Integer
        Property qaacNoDither As Boolean
        Property qaacQuality As Integer = 2
        Property qaacRateMode As Integer

        Property fdkaacProfile As Integer = 2
        Property fdkaacBandwidth As Integer
        Property fdkaacAfterburner As Boolean = True
        Property fdkaacLowDelaySBR As Integer
        Property fdkaacSbrRatio As Integer
        Property fdkaacTransportFormat As Integer
        Property fdkaacGaplessMode As Integer
        Property fdkaacAdtsCrcCheck As Boolean
        Property fdkaacHeaderPeriod As Boolean
        Property fdkaacIncludeSbrDelay As Boolean
        Property fdkaacMoovBeforeMdat As Boolean

        Property ffNormalizeMode As ffNormalizeMode
        Property ffmpegNormalizeMode As ffmpegNormalizeMode

        Property ffmpegLibFdkAAC As Boolean

        Property ffmpegLoudnormIntegrated As Double = -24
        Property ffmpegLoudnormIntegratedMeasured As Double
        Property ffmpegLoudnormLRA As Double = 7
        Property ffmpegLoudnormLraMeasured As Double
        Property ffmpegLoudnormThresholdMeasured As Double
        Property ffmpegLoudnormTruePeak As Double = -2
        Property ffmpegLoudnormTruePeakMeasured As Double

        Property ffmpegDynaudnormF As Integer = 500
        Property ffmpegDynaudnormG As Integer = 31
        Property ffmpegDynaudnormP As Double = 0.95
        Property ffmpegDynaudnormM As Double = 10
        Property ffmpegDynaudnormR As Double
        Property ffmpegDynaudnormN As Boolean = True
        Property ffmpegDynaudnormC As Boolean
        Property ffmpegDynaudnormB As Boolean
        Property ffmpegDynaudnormS As Double

        Property ffmpegOpusApp As OpusApp = OpusApp.audio
        Property ffmpegOpusCompress As Integer = 10
        Property ffmpegOpusFrame As Double = 20
        Property ffmpegOpusMap As Integer = -1
        Property ffmpegOpusPacket As Integer
        Property ffmpegOpusRateMode As OpusRateMode = OpusRateMode.VBR
        Property ffmpegOpusMigrate As Integer = 1

        Property SimpleRateMode As SimpleAudioRateMode
            Get
                If RateMode = AudioRateMode.CBR Then
                    Return SimpleAudioRateMode.CBR
                Else
                    Return SimpleAudioRateMode.VBR
                End If
            End Get
            Set(value As SimpleAudioRateMode)
                If value = SimpleAudioRateMode.CBR Then
                    RateMode = AudioRateMode.CBR
                Else
                    RateMode = AudioRateMode.VBR
                End If
            End Set
        End Property

        'legacy/obsolete
        Sub Migrate()
            '2019
            If fdkaacProfile = 0 Then
                fdkaacProfile = 2
                SimpleRateMode = SimpleAudioRateMode.VBR
                fdkaacAfterburner = True
            End If

            '2019
            If Not Migrate1 Then
                Normalize = True

                ffmpegLoudnormIntegrated = -24
                ffmpegLoudnormLRA = 7
                ffmpegLoudnormTruePeak = -2

                ffmpegDynaudnormF = 500
                ffmpegDynaudnormG = 31
                ffmpegDynaudnormP = 0.95
                ffmpegDynaudnormM = 10
                ffmpegDynaudnormN = True

                Migrate1 = True
            End If

            '2020
            If Not MigrateffNormalizeMode Then
                ffmpegNormalizeMode = CType(ffNormalizeMode, ffmpegNormalizeMode)
                MigrateffNormalizeMode = True
            End If

            '2020
            If ffmpegOpusMigrate <> 1 Then
                ffmpegOpusMigrate = 1
                ffmpegOpusApp = OpusApp.audio
                ffmpegOpusCompress = 10
                ffmpegOpusFrame = 20
                ffmpegOpusMap = -1
                ffmpegOpusRateMode = OpusRateMode.VBR
            End If

            '2020
            If ProbeSize = 0 AndAlso AnalyzeDuration = 0 Then
                ProbeSize = 5
                AnalyzeDuration = 5
            End If
        End Sub
    End Class
End Class

Public Enum AudioCodec
    AAC
    AC3
    DTS
    FLAC
    MP3
    Opus
    Vorbis
    W64
    WAV
    EAC3
    None
End Enum

Public Enum AudioRateMode
    CBR
    ABR
    VBR
End Enum

Public Enum OpusRateMode
    CBR
    VBR
    CVBR
End Enum

Public Enum OpusApp
    voip
    audio
    lowdelay
End Enum

Public Enum SimpleAudioRateMode
    CBR
    VBR
End Enum

Public Enum AudioAacProfile
    Automatic
    LC
    SBR
    <DispName("SBR+PS")> SBRPS = 300
End Enum

Public Enum GuiAudioEncoder
    Automatic
    eac3to
    ffmpeg
    qaac
    fdkaac
End Enum

Public Enum AudioFrameRateMode
    Keep
    <DispName("Apply PAL speedup")> Speedup
    <DispName("Reverse PAL speedup")> Slowdown
End Enum

Public Enum AudioDownMixMode
    <DispName("Simple")> Stereo
    <DispName("Dolby Surround")> Surround
    <DispName("Dolby Surround 2")> Surround2
End Enum

Public Enum ChannelsMode
    Original
    <DispName("1 (Mono)")> _1
    <DispName("2 (Stereo)")> _2
    <DispName("5.1")> _6
    <DispName("6.1")> _7
    <DispName("7.1")> _8
End Enum
