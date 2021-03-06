'==============================================================================
'
' EveHQ - An Eve-Online� character assistance application
' Copyright � 2005-2014  EveHQ Development Team
'
' This file is part of EveHQ.
'
' The source code for EveHQ is free and you may redistribute 
' it and/or modify it under the terms of the MIT License. 
'
' Refer to the NOTICES file in the root folder of EVEHQ source
' project for details of 3rd party components that are covered
' under their own, separate licenses.
'
' EveHQ is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the MIT 
' license below for details.
'
' ------------------------------------------------------------------------------
'
' The MIT License (MIT)
'
' Copyright � 2005-2014  EveHQ Development Team
'
' Permission is hereby granted, free of charge, to any person obtaining a copy
' of this software and associated documentation files (the "Software"), to deal
' in the Software without restriction, including without limitation the rights
' to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
' copies of the Software, and to permit persons to whom the Software is
' furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in
' all copies or substantial portions of the Software.
' 
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
' IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
' FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
' AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
' LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
' OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
' THE SOFTWARE.
'
' ==============================================================================

Imports EveHQ.Core
Imports EveHQ.Common
Imports System.Net.Http
Imports System.Threading.Tasks
Imports EveHQ.Common.Extensions
Imports System.IO

Namespace Forms
    Public Class NewUpdater
        Private ReadOnly _updateLocation As String
        Private ReadOnly _storageFolder As String

        Private ReadOnly _requestProvider As IHttpRequestProvider
        Const DownloadingTemplate As String = "Downloading"
        Const RequestTemplate As String = "Requesting"


        Public Sub New(updateLocation As String, storageFolder As String, provider As IHttpRequestProvider)

            ' This call is required by the designer.
            InitializeComponent()

            ' Add any initialization after the InitializeComponent() call.
            _updateLocation = updateLocation
            _storageFolder = storageFolder
            _requestProvider = provider
        End Sub

        Private Sub newUpdater_Load(sender As Object, e As EventArgs) Handles Me.Load
            _statusHeader.Text = RequestTemplate
            _statusDetail.Text = _updateLocation
        End Sub

        Private Sub DownloadUpdate()
            Try


                Dim localStorage As String = _storageFolder

                Dim task As Task(Of HttpResponseMessage) = _requestProvider.GetAsync(New Uri(_updateLocation), Nothing,
                                                                                     HttpCompletionOption.
                                                                                        ResponseHeadersRead)

                task.ContinueWith(Sub(dlTask As Task(Of HttpResponseMessage))
                    If (dlTask.Exception IsNot Nothing) Then
                        Trace.TraceError(dlTask.Exception.FormatException())
                                     End If

                    If (dlTask.Result.IsSuccessStatusCode = False) Then
                        Trace.TraceWarning(
                            "Update download returned unexpected status {0} {1}".FormatInvariant(
                                dlTask.Result.StatusCode, dlTask.Result.ReasonPhrase))
                                     End If

                    If (dlTask.IsCanceled Or dlTask.Result Is Nothing) Then
                        Exit Sub
                                     End If

                    Dim total As Long
                    Dim current As Double

                    If (dlTask.Result.Content.Headers.ContentLength.HasValue) Then
                        total = dlTask.Result.Content.Headers.ContentLength.Value
                    Else
                        total = 25000000
                                     End If

                    Invoke(Sub()
                        _statusHeader.Text = DownloadingTemplate
                        _statusDetail.Text = _updateLocation
                              End Sub)

                    Try

                        Dim streamTask As Task(Of Stream) = dlTask.Result.Content.ReadAsStreamAsync()
                        streamTask.Wait()

                        If (streamTask.Result IsNot Nothing) Then
                            Dim stream As Stream = streamTask.Result
                            Dim result As List(Of Byte) = New List(Of Byte)
                            Dim readBuffer(500) As Byte
                            Dim readBytes As Int32
                            Do
                                Array.Clear(readBuffer, 0, 500)
                                readBytes = stream.Read(readBuffer, 0, readBuffer.Length)
                                If (readBytes > 0) Then
                                    result.AddRange(readBuffer.Take(readBytes))
                                    current += readBytes
                                    Dim progress As Double = (current/total)*100
                                    Invoke(Sub()
                                        UpdateProgress(progress)
                                              End Sub)
                                     End If
                            Loop While readBytes <> 0
                            streamTask.Result.Close()
                            streamTask.Result.Dispose()

                            Invoke(Sub()
                                _statusDetail.Text = "Saving update file to disk."
                                _statusDetail.TextAlign = ContentAlignment.MiddleCenter
                                      End Sub)


                            Dim savedFilePath As String = Path.Combine(localStorage, GetFileNameFromUrl(_updateLocation))
                            Using _
                                     fs As FileStream =
                                     New FileStream(savedFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)
                                fs.Write(result.ToArray(), 0, result.Count)
                                     End Using

                            Invoke(Sub()
                                _statusDetail.Text = "Download Complete. Click Continue to install update."
                                _statusDetail.TextAlign = ContentAlignment.MiddleCenter
                                _continueButton.Visible = True
                                      End Sub)

                                     End If
                    Catch ex As Exception
                        Trace.TraceError(ex.FormatException)
                        Throw


                                     End Try
                                     End Sub)

            Catch ex As Exception
                Trace.TraceError(ex.FormatException())
                MessageBox.Show(
                    "There was an unexpected error downloading the update. Please look at the log file located at {0}\EveHQLog.log for details, and try your attempt again later" _
                                   .FormatInvariant(Environment.SpecialFolder.ApplicationData))
            End Try
        End Sub

        Private Sub UpdateProgress(progress As Double)

            _downloadProgress.Text = "{0:N1} %".FormatInvariant(progress)
            _downloadProgress.TextVisible = True
            _downloadProgress.Value = CInt(progress)
        End Sub

        Private Shared Function GetFileNameFromUrl(url As String) As String

            Dim lastIndex As Int32 = url.LastIndexOf("/", StringComparison.Ordinal)

            Return url.Substring(lastIndex + 1)
        End Function

        Private Sub newUpdater_Shown(sender As Object, e As EventArgs) Handles Me.Shown
            DownloadUpdate()
        End Sub

        Private Sub RunUpdateInstaller(sender As Object, e As EventArgs) Handles _continueButton.Click
            Dim exeFile As String = GetFileNameFromUrl(_updateLocation)

            Dim arguments As String = ""
            If HQ.IsUsingLocalFolders Then
                arguments = " /local /D=" & System.Environment.CurrentDirectory
            End If

            'exit evehq so the installer will run
            Dim formsToClose As New List(Of String)
            For Each openForm As Form In Application.OpenForms

                formsToClose.Add(openForm.Name)
            Next

            For Each formName As String In formsToClose
                Dim openForm As Form = Application.OpenForms(formName)
                If (openForm IsNot Nothing) Then
                    openForm.Close()
                End If
            Next
            Process.Start(Path.Combine(_storageFolder, exeFile), arguments)
        End Sub
    End Class
End Namespace