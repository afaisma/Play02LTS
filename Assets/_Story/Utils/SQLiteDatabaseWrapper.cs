using System;
using System.Collections.Generic;
using UnityEngine;

/*
 *
Create an SQLiteDatabaseWrapper class using SimpleSQL from Unity Asset store create SQLiteDatabaseWrapper class which:
1. Giving an input file csv such as below creates a SQLLite database (unless it exists already) with the name stories.db also adding a new field page_number 
bool LoadScv(string csvfile). The key for the bool table should be the book_image_url column.
2. implement 
bool DatabaseExists - returns true if the database was created 
3. Store page information
bool SetBookPage(string book_image_url, int page)

Implement csv parsing using either .NET/C# directly or Unity Asset store assets. Do not use Microsoft.VisualBasic.FileIO



Sample input csv: 
book_name,book_author,book_image_url,book_url,age_from,age_to,genre,notes_For_Parents,id
Alphabet Rhymebook,Dr. Stein,Alphabet/images/cover.jpg,Alphabet/Alphabet_chunks_script.txt,2,4,Rhymebooks : Family : Special Education,Learn animals,42
Learn Animals Rhymebook,Dr. Stein,Animals_Rhymebook/images/cover.jpg,Animals_Rhymebook/Animals_Rhymebook_chunks_script.txt,2,4,Rhymebooks : Family : Nature : Special Education,Learn animals,6
Learn Big and Small Rhymes,Dr. Stein,Sizes_Rhymebook/images/cover.jpg,Sizes_Rhymebook/Sizes_Rhymebook_chunks_script.txt,2,4,Rhymebooks : Family : Special Education,Learn Big and Small Rhymebook,9
Learn Colors Rhymebook,Dr. Stein,Colors_Rhymebook/images/cover.jpg,Colors_Rhymebook/Colors_Rhymebook_chunks_script.txt,2,4,Rhymebooks : Family : Special Education,Learn Colors Rhymebook,12
We Wash Things,Dr. Stein,WeWashThings/images/cover.jpg,WeWashThings/WeWashThings_chunks_script.txt,2,4,Family : Special Education,We Wash Things,39
Bedtime Routine Rhymebook,Dr. Stein,BedtimeRoutineRhymebook/images/cover.jpg,BedtimeRoutineRhymebook/BedtimeRoutineRhymebook_chunks_script.txt,3,6,Rhymebooks : Family : Special Education : Manners,Bedtime Routine Rhymebook,1
Learn Birds Rhymebook,Dr. Stein,BirdsRhymebook/images/cover.jpg,BirdsRhymebook/BirdsRhymebook_chunks_script.txt,3,6,Rhymebooks : Family : Special Education : Nature,Learn Birds Rhymebook,10
Farm Animals Rhymebook,Dr. Stein,FarmAnimalsRhymebook/images/cover.jpg,FarmAnimalsRhymebook/FarmAnimalsRhymebook_chunks_script.txt,3,6,Rhymebooks : Family : Special Education : Nature,Farm Animals Rhymebook,14 * 
 */
    


public class SQLiteDatabaseWrapper
{
}
