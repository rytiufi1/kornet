require("dotenv").config();
const express = require("express");
const axios = require("axios");
const path = require("path");
const fs = require("fs");

const app = express();
const cacheFolder = path.join(__dirname, "cache");

if (!fs.existsSync(cacheFolder)) {
    fs.mkdirSync(cacheFolder);
}

app.get("/v1/asset/", async (req, res) => {
    try {
        const id = req.query.id;
        if (!id) return res.status(400).send("Missing id");

        const filePath = path.join(cacheFolder, id);

        if (fs.existsSync(filePath)) {
            console.log(`Serving cached: ${id}`);
            return res.download(filePath, "asset");
        }

        const assetDeliveryApis = [
            "https://assetdelivery.synt2x.xyz/v1/asset", // might require a cookie as danyal wants syntax 2 to be a wannabe roblox. lol..
            "https://assetdelivery.pekora.zip/v1/asset", // ok
            "https://assetdelivery.cartii.fit/v1/asset", // ok
            "https://bt.zawg.ca/v1/asset", // ok
            "https://assetdelivery.roblox.com/v1/asset" // ok
        ];

        const headers = {
            "Cookie": `.ROBLOSECURITY=_|WARNING:-DO-NOT-SHARE-THIS.--Sharing-this-will-allow-someone-to-log-in-as-you-and-to-steal-your-ROBUX-and-items.|_CAEaAhADIhwKBGR1aWQSFDEyMTI1MDIwOTUxNzQ3Nzg3MTkyKAQ.HEKQgvXj7myOt8wOD9SC4-pCHA8NsnElGUNkPGrvIvMxZLp_bQBE40TDajN2wNFxlDsBxJ8t_l_Kcbg-ijtNH3w5EnVEDbQcpPOdbzawxZ6afoketuGsyWfl_8DTZETpBwRaWgZgYQ-hvRl10mZw9vv-vZVYYOLHdgUSgWL5xcg2FzNJU4yArDDgtHLA5OR_PM6Rdj-J71Rpa9tqQZgOoYsIa5MsZeecv_xbFl_jFV326F6BI3ogOMVlmOxpwMmhG9YSGOZDdDB7ASKADx1xkcujonoTL8xWrlQLv-Ivui2MMryHnyX9CBWr-1Sgg7LLcuDDy7Pvx4OohrcGIhMFhX9_gQ8xuMJaDiO47D8cYDi6gLreRuEpuXxBPveVV1FemrtAMv4B1LVCWQpnqNElFS2748nHlrwajgsWfvpIB5klY0IDci5qaMoQBhSJSzdT_ik6MR7nFMcsvDt47gLnLTtuy4PibFJPuqptBYRCKXmtE3MKXvq_qtDcFjApSqnIxZxF5Y2Fhx807Y-Eg8LRdcFbL_zjZBLX6_ko-dSnCOKkYDsnsrI9Xa5gTus50gjLJ58IXz6LOG7a6xXl75dU8uorwb7pQCKQyyRRY5KlfKeYog9Cp2ErLUzW2qLjDA-egXXbwA0lbynp7vD79YPTZyYCcqIKmYuGEnU-_LC0MupxtmVL_Z-Sozt0CbfjC0CigdZbTmKViNDpyGtszRXhQO7_sRtjkZkZ0fh8XJ5EVeSJKmCRt43VoLTBhPgFfYiNkpYnKkzIIhJPFUxTaYVb9L_oIdB8HrpZ8HW_70MFW-xBVxu4LeKysT6VAsh5UvzJ6tdsL2wUWjRNn3KEXtwdwSALvM532Wm4Qcbf0Jt7_i_bYrgkGWNAv9kFN2HEYrSeWVbL59gbev7fIZPHBVXgMG2WudqzW0mcsPLC28OQW8-Vmb-7Fa5k3AQm3v8UtOEJi7rP2A`,
            // "Accept-Encoding": "gzip,deflate,br",
            "Accept": "*/*",
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36" //"Roblox/WinInet"
        };

        let successfulResponse = null;
        let finalurl = null;

        if (process.env.useMultiFetch) {
            const shuffled = assetDeliveryApis.sort(() => 0.5 - Math.random());

            for (const baseUrl of shuffled) {
                try {
                    const check = await axios.get(`${baseUrl}/?id=7702904`, { headers, timeout: 2500 });
                    if (check.status === 200) {
                        successfulResponse = await axios.get(`${baseUrl}/?id=${id}`, {
                            headers,
                            responseType: "stream",
                            timeout: 10000
                        });
                        finalurl = baseUrl
                        break;
                    }
                    console.log(`this fuckin failed by ${baseUrl} with id of ${id}`);
                } catch (err) {
                    continue;
                }
            }
        } else {
            const robloxUrl = `http://assetdelivery.roblox.com/v1/asset/?id=${id}`;
            successfulResponse = await axios.get(robloxUrl, {
                headers,
                responseType: "stream",
                timeout: 10000
            });
            console.log("yay got response i think idk rolox");
        }

        if (!successfulResponse) {
            console.log(successfulResponse);
            console.log("failed 502");
            return res.status(502).send("No asset for u");
        }

        res.setHeader("Content-Disposition", `attachment; filename="asset"`);

        if (process.env.cacheAssets) {
            const writer = fs.createWriteStream(filePath);
            successfulResponse.data.pipe(writer);
        }

        successfulResponse.data.pipe(res);
        console.log(`Serving asset: ${id} from ${finalurl}`);
    } catch (e) {
        console.error(e.message);
        if (!res.headersSent) {
            res.status(500).send("Error fetching asset");
        }
    }
});

app.listen(process.env.Port, () => console.log("Started on Port " + process.env.Port));