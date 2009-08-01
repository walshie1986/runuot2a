#!/bin/sh
gmcs -out:runuo.exe -d:MONO -optimize+ -unsafe -r:System,System.Configuration.Install,System.Data,System.Drawing,System.EnterpriseServices,System.Management,System.Security,System.ServiceProcess,System.Web,System.Web.Services,System.Windows.Forms,System.Xml -nowarn:219 -recurse:Server/*.cs
