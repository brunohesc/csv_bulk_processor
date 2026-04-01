# Flatirons Full-Stack Developer Coding Test

Be sure to read **all** of this document carefully, and follow the guidelines within.

## Context

Implement a full-stack web or mobile application that can upload, process, and store into a database the following CSV filem which contains a list of products. An example CSV file is located in data.csv in this repo.

## Technology Choice
1. **Backend**: Ruby on Rails, NestJS, .NET, or Python (choose the one relevant to your hiring role)
2. **Frontend**:  
   - **Web**: React or Next.js + React  
   - **Mobile**: React Native, Flutter, or Swift (depending on the applied role)

## Requirements - Backend

1. The products should be stored along with multiple exchange rates at the time of the upload utilizing this [API](https://github.com/fawazahmed0/exchange-api) (include at least 5 currencies). All product fields are required and must be present.
2. Implement an endpoint that returns all the processed rows of product data along with the available currency conversions stored at the time of the upload. This endpoint should support filtering and sorting based on the name, price, and expiration fields
4. The application should support CSV files with up to 200k rows, but easily scale to support more.

## Requirements - Frontend
1. The front-end should display a file upload input that allows the user to select a CSV file from their device.
2. While the file is uploading and being processed, there should be a loading indicator displaying progress of the upload.
3. Once the file uploads, a success message should display and you should be able to browse a table of the uploaded products. 

## Submission

1. Create a branch and send a Pull Request into main when you are done. All code must be in this repo as a monorepo. Do not put your solutions in a separate repository!
2. In the pull request, please include a short video walk-through (< 5 minutes) of your code and the experience you built. Also, describe improvement opportunities. [Vidyard](https://www.vidyard.com/chrome-extension-screen-recording/?utm_source=google-ads&utm_medium=cpc&utm_campaign=ChromeExtensionScreenRecord&utm_content=Extention_ChromeExt&utm_term=computer%20screen%20recorder%20free_b&gclid=Cj0KCQiA0eOPBhCGARIsAFIwTs4sn5e2WT7CGOsil0csKejSIthegolcNF2hVsixwJIOXI1zKWW8eO4aAgoVEALw_wcB) is a good choice.
3. [Fill out this form with a link to your Pull Request](https://share.hsforms.com/1U_u8KkLWS6edbYxOoP64Dwse3g0). 

## Questions

If you have any questions, just create a new issue in this repo and we will respond and get back to you quickly.
