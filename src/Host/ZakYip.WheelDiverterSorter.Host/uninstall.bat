set serviceName=ZakYip.WheelDiverterSorter

sc stop   %serviceName% 
sc delete %serviceName% 

pause
