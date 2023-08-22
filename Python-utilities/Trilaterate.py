import numpy                                             
from numpy import sqrt, dot, cross                       
from numpy.linalg import norm     

# This is Python - NOT FOR PROGRAMMABLE BLOCKS

# Get location from 3 known gps points and their distance from observation point
# Enter distance with highest possible accuracy, usually with one decimal
# The script will return 2 locations, either of which could be the right one
# If you get the following error: "The three spheres do not intersect!", make sure all values are correct

def trilaterate(P1,P2,P3,r1,r2,r3):                     
    temp1 = P2-P1                                        
    e_x = temp1/norm(temp1)                              
    temp2 = P3-P1                                        
    i = dot(e_x,temp2)                                   
    temp3 = temp2 - i*e_x                                
    e_y = temp3/norm(temp3)                              
    e_z = cross(e_x,e_y)                                 
    d = norm(P2-P1)                                      
    j = dot(e_y,temp2)                                   
    x = (r1*r1 - r2*r2 + d*d) / (2*d)                    
    y = (r1*r1 - r3*r3 -2*i*x + i*i + j*j) / (2*j)       
    temp4 = r1*r1 - x*x - y*y                            
    if temp4<0:                                          
        print("\nThe three spheres do not intersect!")
        return "Error"
    z = sqrt(temp4)                                      
    p_12_a = P1 + x*e_x + y*e_y + z*e_z                  
    p_12_b = P1 + x*e_x + y*e_y - z*e_z                  
    return p_12_a,p_12_b

while True:
    gpsList = []
    i = 0
    while i < 3:
        try:
            tempDict = {'location':0, 'distance':0}
            tempGps = input("Enter GPS "+str(i+1)+": ").split(':')
            tempDict['location'] = numpy.array([float(tempGps[2]), float(tempGps[3]), float(tempGps[4])])

            tempDis = input("Enter distance "+str(i+1)+" (in kms): ")
            tempDict['distance'] = (float(tempDis)+0.1)*1000
            gpsList.append(tempDict)
            i = i+1   
        except:
            print("Invalid GPS or distance.")                    

    intersect = trilaterate(gpsList[0]['location'],gpsList[1]['location'],gpsList[2]['location'],gpsList[0]['distance'],gpsList[1]['distance'],gpsList[2]['distance'])
    pointNum = 1
    print("")
    for point in intersect:
        if intersect == "Error":
            break
        print("GPS:Intersect "+str(pointNum)+":"+str(point[0])+":"+str(point[1])+":"+str(point[2])+":#FFFFFFFF:")
        pointNum = pointNum+1
    input("\nPress enter to continue.\n")
