/*
 * English (Roman) → Hindi (Devanagari) for names, e.g. "shri mahesh ji" → "श्री महेश जी".
 * Used by the Congratulations page. The server asks Google Input Tools when it can
 * reach it; this built-in converter is used for any word the server could not convert,
 * so the feature also works offline. The result is always editable by the user.
 */
(function (global) {
    'use strict';

    // Titles, joining words and common surnames: written the usual way.
    var WORDS = {
        shri: 'श्री', shree: 'श्री', sri: 'श्री', shriman: 'श्रीमान', mr: 'श्री',
        smt: 'श्रीमती', shrimati: 'श्रीमती', shreemati: 'श्रीमती', srimati: 'श्रीमती', mrs: 'श्रीमती',
        ms: 'सुश्री', sushri: 'सुश्री', kumari: 'कुमारी', km: 'कुमारी', kum: 'कुमारी',
        dr: 'डॉ.', doctor: 'डॉक्टर', prof: 'प्रो.', professor: 'प्रोफेसर', er: 'इं.',
        ji: 'जी', jee: 'जी', evam: 'एवं', and: 'एवं', priya: 'प्रिय', sir: 'सर', madam: 'मैडम', mam: 'मैडम',
        sahab: 'साहब', saheb: 'साहब', bhai: 'भाई', bhaiya: 'भैया', didi: 'दीदी', ben: 'बेन', behan: 'बहन',
        devi: 'देवी', kumar: 'कुमार', lal: 'लाल', chand: 'चंद', prasad: 'प्रसाद', nath: 'नाथ', das: 'दास',
        sharma: 'शर्मा', gupta: 'गुप्ता', jain: 'जैन', agrawal: 'अग्रवाल', agarwal: 'अग्रवाल', aggarwal: 'अग्रवाल',
        verma: 'वर्मा', varma: 'वर्मा', singh: 'सिंह', patel: 'पटेल', mishra: 'मिश्रा', tiwari: 'तिवारी',
        tripathi: 'त्रिपाठी', pandey: 'पांडे', pande: 'पांडे', dubey: 'दुबे', dwivedi: 'द्विवेदी', chaturvedi: 'चतुर्वेदी',
        trivedi: 'त्रिवेदी', joshi: 'जोशी', yadav: 'यादव', chauhan: 'चौहान', rathore: 'राठौर', thakur: 'ठाकुर',
        shukla: 'शुक्ला', srivastava: 'श्रीवास्तव', shrivastava: 'श्रीवास्तव', saxena: 'सक्सेना', mehta: 'मेहता',
        shah: 'शाह', kapoor: 'कपूर', khanna: 'खन्ना', malhotra: 'मल्होत्रा', chopra: 'चोपड़ा', bansal: 'बंसल',
        goyal: 'गोयल', goel: 'गोयल', mittal: 'मित्तल', garg: 'गर्ग', maheshwari: 'माहेश्वरी', khandelwal: 'खंडेलवाल',
        kulkarni: 'कुलकर्णी', deshpande: 'देशपांडे', nair: 'नायर', iyer: 'अय्यर', reddy: 'रेड्डी', rao: 'राव',
        chetna: 'चेतना', rekha: 'रेखा', krishna: 'कृष्ण', krishan: 'कृष्ण', lakshmi: 'लक्ष्मी', laxmi: 'लक्ष्मी',
        ram: 'राम', raj: 'राज', rajesh: 'राजेश', rakesh: 'राकेश', ramesh: 'रमेश', suresh: 'सुरेश', mahesh: 'महेश',
        dinesh: 'दिनेश', ganesh: 'गणेश', mukesh: 'मुकेश', naresh: 'नरेश', rajendra: 'राजेंद्र', narendra: 'नरेंद्र',
        surendra: 'सुरेंद्र', mahendra: 'महेंद्र', vijay: 'विजय', sanjay: 'संजय', ajay: 'अजय', anil: 'अनिल', sunil: 'सुनील',
        amit: 'अमित', sumit: 'सुमित', rohit: 'रोहित', mohit: 'मोहित', vikas: 'विकास', sunita: 'सुनीता', anita: 'अनीता',
        kavita: 'कविता', savita: 'सविता', geeta: 'गीता', gita: 'गीता', seema: 'सीमा', neha: 'नेहा', pooja: 'पूजा', puja: 'पूजा',
        priyanka: 'प्रियंका', ravi: 'रवि', shruti: 'श्रुति', swati: 'स्वाति', jyoti: 'ज्योति', aarti: 'आरती', arti: 'आरती',
        meera: 'मीरा', mira: 'मीरा', radha: 'राधा', sita: 'सीता', usha: 'उषा', asha: 'आशा', nisha: 'निशा', manisha: 'मनीषा'
    };

    var VOWELS = [ // [roman, independent, sign after a consonant]
        ['aa', 'आ', 'ा'], ['ai', 'ऐ', 'ै'], ['au', 'औ', 'ौ'], ['ee', 'ई', 'ी'], ['ii', 'ई', 'ी'],
        ['oo', 'ऊ', 'ू'], ['uu', 'ऊ', 'ू'], ['a', 'अ', ''], ['i', 'इ', 'ि'], ['u', 'उ', 'ु'], ['e', 'ए', 'े'], ['o', 'ओ', 'ो']
    ];

    var CONSONANTS = [
        ['ksh', 'क्ष'], ['chh', 'छ'], ['gy', 'ज्ञ'], ['ch', 'च'], ['kh', 'ख'], ['gh', 'घ'], ['jh', 'झ'], ['th', 'थ'],
        ['dh', 'ध'], ['ph', 'फ'], ['bh', 'भ'], ['sh', 'श'], ['tt', 'ट्ट'], ['dd', 'ड्ड'],
        ['k', 'क'], ['g', 'ग'], ['c', 'क'], ['j', 'ज'], ['t', 'त'], ['d', 'द'], ['n', 'न'], ['p', 'प'], ['b', 'ब'],
        ['m', 'म'], ['y', 'य'], ['r', 'र'], ['l', 'ल'], ['v', 'व'], ['w', 'व'], ['s', 'स'], ['h', 'ह'],
        ['f', 'फ़'], ['z', 'ज़'], ['q', 'क'], ['x', 'क्स']
    ];

    function match(list, s, i) {
        for (var k = 0; k < list.length; k++) if (s.substr(i, list[k][0].length) === list[k][0]) return list[k];
        return null;
    }

    // Two consonants written together (with ्): r + any (Sharma), any + r / y / v (Priya, Swati),
    // s/sh + t, p, k, m, n, w (Ishwar), p + t (Gupta), and doubled letters.
    function joined(first, second) {
        if (first === 'r' || second === 'r' || second === 'y' || second === 'v' || second === 'w') return true;
        if (first === second) return true;
        if ((first === 's' || first === 'sh' || first === 'ksh') && /^(t|th|p|k|m|n|w|v)$/.test(second)) return true;
        return first === 'p' && second === 't';
    }

    function word(w) {
        var s = w.toLowerCase();
        if (WORDS[s]) return WORDS[s];
        // A final y after a consonant sounds like ee (Chaudhary → चौधरी).
        if (/[^aeiou]y$/.test(s)) s = s.slice(0, -1) + 'ee';

        var out = '', i = 0, pending = null; // pending: roman consonant still without a vowel
        var cluster = false;                  // the pending consonant is joined to the one before it
        while (i < s.length) {
            var c = match(CONSONANTS, s, i);
            if (c) {
                var next = s.substr(i + c[0].length);
                var nextIsConsonant = next.length > 0 && !match(VOWELS, next, 0);
                // n / m before another consonant becomes the dot (अंश, संजय, अंबिका).
                if ((c[0] === 'n' || c[0] === 'm') && !pending && out && nextIsConsonant && !/^[mnyrlvwh]/.test(next)) {
                    out += 'ं'; i += 1; continue;
                }
                cluster = !!(pending && joined(pending, c[0]));
                if (cluster) out += '्';
                out += c[1]; pending = c[0]; i += c[0].length;
                continue;
            }
            var v = match(VOWELS, s, i);
            if (v) {
                var end = i + v[0].length === s.length;
                if (pending) {
                    // A name ending in a / i is written with ा / ी (Rekha, Gattani),
                    // but not after a joined r / y (Chandra → चंद्र, Aditya → आदित्य).
                    var keepShort = cluster && (pending === 'r' || pending === 'y');
                    // A silent final e (Kishore → किशोर) is dropped in longer names.
                    out += end && v[0] === 'a' && !keepShort ? 'ा' : end && v[0] === 'i' ? 'ी' : end && v[0] === 'e' && s.length > 4 ? '' : v[2];
                } else out += v[1];
                pending = null; cluster = false; i += v[0].length;
                continue;
            }
            out += s.charAt(i); pending = null; cluster = false; i++;
        }
        return out;
    }

    /** Converts every English word in the text; Hindi and other characters stay as they are. */
    function text(value, known) {
        return String(value || '').replace(/(^|\s)&(?=\s|$)/g, '$1एवं').replace(/[A-Za-z]+\.?/g, function (token) {
            var dot = token.charAt(token.length - 1) === '.';
            var w = dot ? token.slice(0, -1) : token;
            var lw = w.toLowerCase();
            // Titles from the list are used as written (Dr. → डॉ., Mrs. → श्रीमती).
            if (WORDS[lw]) return WORDS[lw];
            var hi = (known && known[lw]) || word(w);
            return hi + (dot ? '.' : '');
        });
    }

    global.HindiTranslit = { word: word, text: text, isListed: function (w) { return WORDS.hasOwnProperty(String(w).toLowerCase()); } };
})(window);
