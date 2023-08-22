import numpy                                             

# This is Python - NOT FOR PROGRAMMABLE BLOCKS

# Get average location of gps points (Strong unknown signals)
# Entering more will increase the accuracy, you need 2 at the very least
# Make sure every unknown signal you enter has spawned around the same location (player) in space

def average(gpsList):                     
    totalGps = numpy.array([float(0),float(0),float(0)])
    for gps in gpsList:
        totalGps += gps
    return totalGps/len(gpsList)

gpsList = []
i = 1

while True:
    if i < 3:
        arginfo = ": "
    else:
        arginfo = " or press enter to get average: "
    arg = input("Enter GPS "+str(i)+arginfo)
    if(arg != "" or i < 3):
        try:
            tempGps = arg.split(':')
            gpsList.append(numpy.array([float(tempGps[2]), float(tempGps[3]), float(tempGps[4])]))
            i += 1
        except:
            print("Invalid GPS or distance.")       
    else:
        avgGps = average(gpsList)   
        print("\nGPS:Average:"+str(avgGps[0])+":"+str(avgGps[1])+":"+str(avgGps[2])+":#FFFFFFFF:")
        gpsList = []    
        i = 1   
        input("\nPress enter to continue.\n")
