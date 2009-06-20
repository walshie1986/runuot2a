If EXIST server.exe del server.exe
%WINDIR%\microsoft.net\Framework\v2.0.50727\csc.exe /unsafe /out:Server.exe /recurse:server\*.cs /win32icon:Server\runuo.ico